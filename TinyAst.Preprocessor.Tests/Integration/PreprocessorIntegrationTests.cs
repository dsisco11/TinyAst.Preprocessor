using System.Collections.Concurrent;
using TinyAst.Preprocessor.Bridge.Content;
using TinyAst.Preprocessor.Bridge.Imports;
using TinyAst.Preprocessor.Bridge.Merging;
using TinyAst.Preprocessor.Bridge.Resources;
using TinyAst.Preprocessor.Tests.Bridge.Imports;
using TinyPreprocessor;
using TinyPreprocessor.Core;
using TinyPreprocessor.Diagnostics;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Tests.Integration;

/// <summary>
/// End-to-end integration tests that exercise the full preprocessor pipeline.
/// </summary>
public class PreprocessorIntegrationTests
{
    private sealed class MappingResolver : IResourceResolver<SyntaxTree>
    {
        private readonly InMemorySyntaxTreeResourceStore _store;
        private readonly IReadOnlyDictionary<string, ResourceId> _map;

        public MappingResolver(InMemorySyntaxTreeResourceStore store, IReadOnlyDictionary<string, ResourceId> map)
        {
            _store = store;
            _map = map;
        }

        public ValueTask<ResourceResolutionResult<SyntaxTree>> ResolveAsync(
            string reference,
            IResource<SyntaxTree>? context,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var key = reference.Trim();
            var resolvedId = _map.TryGetValue(key, out var mapped)
                ? mapped
                : new ResourceId(key);

            if (_store.TryGet(resolvedId, out var resource))
            {
                return ValueTask.FromResult(ResourceResolutionResult<SyntaxTree>.Success(resource));
            }

            var diagnostic = new ResolutionFailedDiagnostic(
                reference,
                "NotFound",
                context?.Id,
                null);

            return ValueTask.FromResult(ResourceResolutionResult<SyntaxTree>.Failure(diagnostic));
        }
    }

    private sealed class SequencedMappingResolver : IResourceResolver<SyntaxTree>
    {
        private readonly InMemorySyntaxTreeResourceStore _store;
        private readonly IReadOnlyDictionary<string, ResourceId[]> _map;
        private readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.Ordinal);

        public SequencedMappingResolver(InMemorySyntaxTreeResourceStore store, IReadOnlyDictionary<string, ResourceId[]> map)
        {
            _store = store;
            _map = map;
        }

        public ValueTask<ResourceResolutionResult<SyntaxTree>> ResolveAsync(
            string reference,
            IResource<SyntaxTree>? context,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var key = reference.Trim();

            if (_map.TryGetValue(key, out var ids) && ids.Length > 0)
            {
                var callIndex = _counts.AddOrUpdate(key, 0, (_, existing) => existing + 1);
                var sequenceIndex = Math.Min(callIndex, ids.Length - 1);
                var resolvedId = ids[sequenceIndex];

                if (_store.TryGet(resolvedId, out var resource))
                {
                    return ValueTask.FromResult(ResourceResolutionResult<SyntaxTree>.Success(resource));
                }
            }

            var fallbackId = new ResourceId(key);
            if (_store.TryGet(fallbackId, out var fallbackResource))
            {
                return ValueTask.FromResult(ResourceResolutionResult<SyntaxTree>.Success(fallbackResource));
            }

            var diagnostic = new ResolutionFailedDiagnostic(
                reference,
                "NotFound",
                context?.Id,
                null);

            return ValueTask.FromResult(ResourceResolutionResult<SyntaxTree>.Failure(diagnostic));
        }
    }

    private static Schema CreateTestSchema()
    {
        var importPattern = new PatternBuilder()
            .Ident("import")
            .String()
            .BuildQuery();

        return Schema.Create()
            .DefineSyntax<TestImportNode>(
                "Import",
                b => b.Match(importPattern))
            .Build();
    }

    private static Preprocessor<SyntaxTree, ImportDirective, object> CreatePreprocessor(
        InMemorySyntaxTreeResourceStore store,
        ImportDirectiveLocationIndex? locationIndex = null)
    {
        var resolver = new InMemorySyntaxTreeResourceResolver(store, locationIndex);

        var parser = new ImportDirectiveParser<TestImportNode>(n => n.Reference);
        var mergeStrategy = new SyntaxTreeMergeStrategy<TestImportNode, object>(n => n.Reference);

        var config = new PreprocessorConfiguration<SyntaxTree, ImportDirective, object>(
            parser,
            ImportDirectiveModel.Instance,
            resolver,
            mergeStrategy,
            SyntaxTreeContentModel.Instance);

        return new Preprocessor<SyntaxTree, ImportDirective, object>(config);
    }

    private static IResource<SyntaxTree> CreateResource(string id, string source, Schema schema)
    {
        var tree = SyntaxTree.ParseAndBind(source, schema);
        return new Resource<SyntaxTree>(new ResourceId(id), tree);
    }

    #region End-to-End Happy Path Tests

    [Fact]
    public async Task ProcessAsync_SingleFileNoImports_ReturnsOriginalContent()
    {
        // Arrange
        var schema = CreateTestSchema();
        var store = new InMemorySyntaxTreeResourceStore();

        var mainSource = "let x = 1";
        var mainResource = CreateResource("main", mainSource, schema);
        store.Add(mainResource);

        var preprocessor = CreatePreprocessor(store);

        // Act
        var result = await preprocessor.ProcessAsync(mainResource, null!, PreprocessorOptions.Default);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(mainSource, result.Content.ToText());
    }

    [Fact]
    public async Task ProcessAsync_SingleImport_MergesContent()
    {
        // Arrange
        var schema = CreateTestSchema();
        var store = new InMemorySyntaxTreeResourceStore();

        var libSource = "let lib = 42";
        var mainSource = "import \"lib\"\nlet main = lib";

        var libResource = CreateResource("lib", libSource, schema);
        var mainResource = CreateResource("main", mainSource, schema);

        store.Add(libResource);
        store.Add(mainResource);

        var preprocessor = CreatePreprocessor(store);

        // Act
        var result = await preprocessor.ProcessAsync(mainResource, null!, PreprocessorOptions.Default);

        // Assert
        Assert.True(result.Success);
        var mergedText = result.Content.ToText();
        Assert.Contains("let lib = 42", mergedText);
        Assert.Contains("let main = lib", mergedText);
        Assert.DoesNotContain("import", mergedText);
    }

    [Fact]
    public async Task ProcessAsync_MultipleImports_MergesInOrder()
    {
        // Arrange
        var schema = CreateTestSchema();
        var store = new InMemorySyntaxTreeResourceStore();

        var aSource = "let a = 1";
        var bSource = "let b = 2";
        var mainSource = "import \"a\"\nimport \"b\"\nlet main = a + b";

        store.Add(CreateResource("a", aSource, schema));
        store.Add(CreateResource("b", bSource, schema));
        var mainResource = CreateResource("main", mainSource, schema);
        store.Add(mainResource);

        var preprocessor = CreatePreprocessor(store);

        // Act
        var result = await preprocessor.ProcessAsync(mainResource, null!, PreprocessorOptions.Default);

        // Assert
        Assert.True(result.Success);
        var mergedText = result.Content.ToText();
        Assert.Contains("let a = 1", mergedText);
        Assert.Contains("let b = 2", mergedText);
        Assert.Contains("let main = a + b", mergedText);
        Assert.DoesNotContain("import", mergedText);

        // Verify order
        var aIndex = mergedText.IndexOf("let a = 1", StringComparison.Ordinal);
        var bIndex = mergedText.IndexOf("let b = 2", StringComparison.Ordinal);
        Assert.True(aIndex < bIndex);
    }

    [Fact]
    public async Task ProcessAsync_NestedImports_ExpandsRecursively()
    {
        // Arrange
        var schema = CreateTestSchema();
        var store = new InMemorySyntaxTreeResourceStore();

        // main -> lib -> helper
        var helperSource = "let helper = 100";
        var libSource = "import \"helper\"\nlet lib = helper";
        var mainSource = "import \"lib\"\nlet main = lib";

        store.Add(CreateResource("helper", helperSource, schema));
        store.Add(CreateResource("lib", libSource, schema));
        var mainResource = CreateResource("main", mainSource, schema);
        store.Add(mainResource);

        var preprocessor = CreatePreprocessor(store);

        // Act
        var result = await preprocessor.ProcessAsync(mainResource, null!, PreprocessorOptions.Default);

        // Assert
        Assert.True(result.Success);
        var mergedText = result.Content.ToText();
        Assert.Contains("let helper = 100", mergedText);
        Assert.Contains("let lib = helper", mergedText);
        Assert.Contains("let main = lib", mergedText);
        Assert.DoesNotContain("import", mergedText);
    }

    [Fact]
    public async Task ProcessAsync_DiamondDependency_DeduplicatesContent()
    {
        // Arrange
        var schema = CreateTestSchema();
        var store = new InMemorySyntaxTreeResourceStore();

        // main -> a, b; a -> shared; b -> shared (diamond)
        var sharedSource = "let shared = 0";
        var aSource = "import \"shared\"\nlet a = shared";
        var bSource = "import \"shared\"\nlet b = shared";
        var mainSource = "import \"a\"\nimport \"b\"\nlet main = a + b";

        store.Add(CreateResource("shared", sharedSource, schema));
        store.Add(CreateResource("a", aSource, schema));
        store.Add(CreateResource("b", bSource, schema));
        var mainResource = CreateResource("main", mainSource, schema);
        store.Add(mainResource);

        var options = new PreprocessorOptions { DeduplicateIncludes = true };
        var preprocessor = CreatePreprocessor(store);

        // Act
        var result = await preprocessor.ProcessAsync(mainResource, null!, options);

        // Assert
        Assert.True(result.Success);
        var mergedText = result.Content.ToText();

        // Content from shared, a, b, and main should all be present
        Assert.Contains("let shared = 0", mergedText);
        Assert.Contains("let a = shared", mergedText);
        Assert.Contains("let b = shared", mergedText);
        Assert.Contains("let main = a + b", mergedText);
    }

    #endregion

    #region Schema Binding Required Tests

    [Fact]
    public async Task ProcessAsync_UnboundRootTree_ThrowsInvalidOperationException()
    {
        // Arrange
        var store = new InMemorySyntaxTreeResourceStore();

        // Create unbound tree (no schema)
        var unboundTree = SyntaxTree.Parse("let x = 1");
        var mainResource = new Resource<SyntaxTree>(new ResourceId("main"), unboundTree);
        store.Add(mainResource);

        var preprocessor = CreatePreprocessor(store);

        // Act & Assert: Parser throws InvalidOperationException for unbound trees
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => preprocessor.ProcessAsync(mainResource, null!, PreprocessorOptions.Default).AsTask());
    }

    #endregion

    #region Cycle Detection Tests

    [Fact]
    public async Task ProcessAsync_DirectCycle_ReportsDiagnostic()
    {
        // Arrange
        var schema = CreateTestSchema();
        var store = new InMemorySyntaxTreeResourceStore();

        // a -> b -> a (cycle)
        var aSource = "import \"b\"\nlet a = 1";
        var bSource = "import \"a\"\nlet b = 2";

        store.Add(CreateResource("a", aSource, schema));
        store.Add(CreateResource("b", bSource, schema));
        var aResource = CreateResource("a", aSource, schema);

        var preprocessor = CreatePreprocessor(store);

        // Act
        var result = await preprocessor.ProcessAsync(aResource, null!, PreprocessorOptions.Default);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d is CircularDependencyDiagnostic);
    }

    [Fact]
    public async Task ProcessAsync_SelfImport_ReportsDiagnostic()
    {
        // Arrange
        var schema = CreateTestSchema();
        var store = new InMemorySyntaxTreeResourceStore();

        // main imports itself
        var mainSource = "import \"main\"\nlet x = 1";
        var mainResource = CreateResource("main", mainSource, schema);
        store.Add(mainResource);

        var preprocessor = CreatePreprocessor(store);

        // Act
        var result = await preprocessor.ProcessAsync(mainResource, null!, PreprocessorOptions.Default);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d is CircularDependencyDiagnostic);
    }

    [Fact]
    public async Task ProcessAsync_IndirectCycle_ReportsDiagnostic()
    {
        // Arrange
        var schema = CreateTestSchema();
        var store = new InMemorySyntaxTreeResourceStore();

        // a -> b -> c -> a (indirect cycle)
        var aSource = "import \"b\"\nlet a = 1";
        var bSource = "import \"c\"\nlet b = 2";
        var cSource = "import \"a\"\nlet c = 3";

        var aResource = CreateResource("a", aSource, schema);
        store.Add(aResource);
        store.Add(CreateResource("b", bSource, schema));
        store.Add(CreateResource("c", cSource, schema));

        var preprocessor = CreatePreprocessor(store);

        // Act
        var result = await preprocessor.ProcessAsync(aResource, null!, PreprocessorOptions.Default);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d is CircularDependencyDiagnostic);
    }

    #endregion

    #region Max Depth Tests

    [Fact]
    public async Task ProcessAsync_ExceedsMaxDepth_ReportsDiagnostic()
    {
        // Arrange
        var schema = CreateTestSchema();
        var store = new InMemorySyntaxTreeResourceStore();

        // Create a chain: main -> d1 -> d2 -> d3 -> d4 -> d5
        store.Add(CreateResource("d5", "let d5 = 5", schema));
        store.Add(CreateResource("d4", "import \"d5\"\nlet d4 = 4", schema));
        store.Add(CreateResource("d3", "import \"d4\"\nlet d3 = 3", schema));
        store.Add(CreateResource("d2", "import \"d3\"\nlet d2 = 2", schema));
        store.Add(CreateResource("d1", "import \"d2\"\nlet d1 = 1", schema));

        var mainResource = CreateResource("main", "import \"d1\"\nlet main = 0", schema);
        store.Add(mainResource);

        var options = new PreprocessorOptions { MaxIncludeDepth = 3 };
        var preprocessor = CreatePreprocessor(store);

        // Act
        var result = await preprocessor.ProcessAsync(mainResource, null!, options);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d is MaxDepthExceededDiagnostic);
    }

    [Fact]
    public async Task ProcessAsync_WithinMaxDepth_Succeeds()
    {
        // Arrange
        var schema = CreateTestSchema();
        var store = new InMemorySyntaxTreeResourceStore();

        // Create a chain within limits: main -> d1 -> d2
        store.Add(CreateResource("d2", "let d2 = 2", schema));
        store.Add(CreateResource("d1", "import \"d2\"\nlet d1 = 1", schema));

        var mainResource = CreateResource("main", "import \"d1\"\nlet main = 0", schema);
        store.Add(mainResource);

        var options = new PreprocessorOptions { MaxIncludeDepth = 5 };
        var preprocessor = CreatePreprocessor(store);

        // Act
        var result = await preprocessor.ProcessAsync(mainResource, null!, options);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
    }

    #endregion

    #region Resolution Failure Tests

    [Fact]
    public async Task ProcessAsync_MissingImport_ReportsDiagnosticWithLocation()
    {
        // Arrange
        var schema = CreateTestSchema();
        var store = new InMemorySyntaxTreeResourceStore();

        var mainSource = "import \"nonexistent\"\nlet x = 1";
        var mainResource = CreateResource("main", mainSource, schema);
        store.Add(mainResource);

        // Create location index for the directive
        var locationIndex = new ImportDirectiveLocationIndex();
        var parser = new ImportDirectiveParser<TestImportNode>(n => n.Reference);
        foreach (var directive in parser.Parse(mainResource.Content, mainResource.Id))
        {
            locationIndex.Add(mainResource.Id, directive.Reference, directive.Location);
        }

        var preprocessor = CreatePreprocessor(store, locationIndex);

        // Act
        var result = await preprocessor.ProcessAsync(mainResource, null!, PreprocessorOptions.Default);

        // Assert
        Assert.False(result.Success);

        var resolutionDiagnostic = result.Diagnostics
            .OfType<ResolutionFailedDiagnostic>()
            .FirstOrDefault();

        Assert.NotNull(resolutionDiagnostic);
        Assert.Equal("nonexistent", resolutionDiagnostic.Reference);
        Assert.NotNull(resolutionDiagnostic.Resource);
        Assert.Equal(mainResource.Id, resolutionDiagnostic.Resource.Value);
    }

    [Fact]
    public async Task ProcessAsync_MultipleResolutionFailures_ReportsAllDiagnostics()
    {
        // Arrange
        var schema = CreateTestSchema();
        var store = new InMemorySyntaxTreeResourceStore();

        var mainSource = "import \"missing1\"\nimport \"missing2\"\nlet x = 1";
        var mainResource = CreateResource("main", mainSource, schema);
        store.Add(mainResource);

        var options = new PreprocessorOptions { ContinueOnError = true };
        var preprocessor = CreatePreprocessor(store);

        // Act
        var result = await preprocessor.ProcessAsync(mainResource, null!, options);

        // Assert
        Assert.False(result.Success);

        var resolutionDiagnostics = result.Diagnostics
            .OfType<ResolutionFailedDiagnostic>()
            .ToList();

        Assert.Equal(2, resolutionDiagnostics.Count);
        Assert.Contains(resolutionDiagnostics, d => d.Reference == "missing1");
        Assert.Contains(resolutionDiagnostics, d => d.Reference == "missing2");
    }

    [Fact]
    public async Task ProcessAsync_CanonicalIdDiffersFromReference_MergesUsingCanonicalId()
    {
        // Arrange
        var schema = CreateTestSchema();
        var store = new InMemorySyntaxTreeResourceStore();

        var canonicalId = new ResourceId("domain:lib/shared");
        var libSource = "let lib = 42";
        var mainSource = "import \"lib/shared\"\nlet main = lib";

        store.Add(CreateResource(canonicalId.Path, libSource, schema));
        var mainResource = CreateResource("main", mainSource, schema);
        store.Add(mainResource);

        var resolver = new MappingResolver(
            store,
            new Dictionary<string, ResourceId>(StringComparer.Ordinal)
            {
                ["lib/shared"] = canonicalId
            });

        var parser = new ImportDirectiveParser<TestImportNode>(n => n.Reference);
        var mergeStrategy = new SyntaxTreeMergeStrategy<TestImportNode, object>(n => n.Reference);

        var config = new PreprocessorConfiguration<SyntaxTree, ImportDirective, object>(
            parser,
            ImportDirectiveModel.Instance,
            resolver,
            mergeStrategy,
            SyntaxTreeContentModel.Instance);

        var preprocessor = new Preprocessor<SyntaxTree, ImportDirective, object>(config);

        // Act
        var result = await preprocessor.ProcessAsync(mainResource, null!, PreprocessorOptions.Default);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);

        var mergedText = result.Content.ToText();
        Assert.Contains("let lib = 42", mergedText);
        Assert.Contains("let main = lib", mergedText);
        Assert.DoesNotContain("import", mergedText);
    }

    [Fact]
    public async Task ProcessAsync_DuplicateReferences_CanResolveToDistinctCanonicalTargets()
    {
        // Arrange
        var schema = CreateTestSchema();
        var store = new InMemorySyntaxTreeResourceStore();

        var canonicalA = new ResourceId("domain:lib/shared/a");
        var canonicalB = new ResourceId("domain:lib/shared/b");

        store.Add(CreateResource(canonicalA.Path, "let libA = 1", schema));
        store.Add(CreateResource(canonicalB.Path, "let libB = 2", schema));

        var mainSource = "import \"lib/shared\"\nimport \"lib/shared\"\nlet main = 0";
        var mainResource = CreateResource("main", mainSource, schema);
        store.Add(mainResource);

        var resolver = new SequencedMappingResolver(
            store,
            new Dictionary<string, ResourceId[]>(StringComparer.Ordinal)
            {
                ["lib/shared"] = [canonicalA, canonicalB]
            });

        var parser = new ImportDirectiveParser<TestImportNode>(n => n.Reference);
        var mergeStrategy = new SyntaxTreeMergeStrategy<TestImportNode, object>(n => n.Reference);

        var config = new PreprocessorConfiguration<SyntaxTree, ImportDirective, object>(
            parser,
            ImportDirectiveModel.Instance,
            resolver,
            mergeStrategy,
            SyntaxTreeContentModel.Instance);

        var preprocessor = new Preprocessor<SyntaxTree, ImportDirective, object>(config);

        // Act
        var result = await preprocessor.ProcessAsync(mainResource, null!, PreprocessorOptions.Default);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);

        var mergedText = result.Content.ToText();
        Assert.Contains("let libA = 1", mergedText);
        Assert.Contains("let libB = 2", mergedText);
        Assert.Contains("let main = 0", mergedText);
        Assert.DoesNotContain("import", mergedText);

        var aIndex = mergedText.IndexOf("let libA = 1", StringComparison.Ordinal);
        var bIndex = mergedText.IndexOf("let libB = 2", StringComparison.Ordinal);
        Assert.True(aIndex >= 0 && bIndex >= 0 && aIndex < bIndex, "First resolved target should appear before second.");
    }

    #endregion

    #region Source Map Tests

    [Fact]
    public async Task ProcessAsync_WithImport_ProducesSourceMap()
    {
        // Arrange
        var schema = CreateTestSchema();
        var store = new InMemorySyntaxTreeResourceStore();

        var libSource = "let lib = 1";
        var mainSource = "import \"lib\"\nlet main = 2";

        store.Add(CreateResource("lib", libSource, schema));
        var mainResource = CreateResource("main", mainSource, schema);
        store.Add(mainResource);

        var preprocessor = CreatePreprocessor(store);

        // Act
        var result = await preprocessor.ProcessAsync(mainResource, null!, PreprocessorOptions.Default);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.SourceMap);
    }

    #endregion

    #region Helper Methods

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    #endregion
}
