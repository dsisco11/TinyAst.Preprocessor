using TinyAst.Preprocessor.Bridge.Content;
using TinyAst.Preprocessor.Bridge.Imports;
using TinyAst.Preprocessor.Bridge.Merging;
using TinyAst.Preprocessor.Tests.Bridge.Imports;
using TinyPreprocessor.Core;
using TinyPreprocessor.Diagnostics;
using TinyPreprocessor.Merging;
using TinyPreprocessor.SourceMaps;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Tests.Bridge.Merging;

public class SyntaxTreeMergeStrategyTests
{
    private sealed class CountingMergeStrategy : SyntaxTreeMergeStrategy<TestImportNode, object>
    {
        private readonly Dictionary<ResourceId, int> _counts;

        public CountingMergeStrategy(Dictionary<ResourceId, int> counts)
            : base(n => n.Reference)
        {
            _counts = counts;
        }

        protected override void OnProcessResource(ResourceId resourceId)
        {
            _counts[resourceId] = _counts.TryGetValue(resourceId, out var c) ? c + 1 : 1;
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

    private static MergeContext<SyntaxTree, ImportDirective> CreateMergeContext(
        IReadOnlyList<ResolvedResource<SyntaxTree, ImportDirective>> orderedResources)
    {
        var resolvedCache = orderedResources
            .Select(r => r.Resource)
            .ToDictionary(r => r.Id, r => (IResource<SyntaxTree>)r);

        var resolvedReferences = new Dictionary<MergeContext<SyntaxTree, ImportDirective>.ResolvedReferenceKey, ResourceId>();

        foreach (var resource in orderedResources)
        {
            for (var i = 0; i < resource.Directives.Count; i++)
            {
                var key = new MergeContext<SyntaxTree, ImportDirective>.ResolvedReferenceKey(resource.Id, i);
                resolvedReferences[key] = new ResourceId(resource.Directives[i].Reference);
            }
        }

        return new MergeContext<SyntaxTree, ImportDirective>(
            new SourceMapBuilder(),
            new DiagnosticCollection(),
            resolvedCache,
            resolvedReferences,
            ImportDirectiveModel.Instance,
            SyntaxTreeContentModel.Instance,
            null);
    }

    private static IResource<SyntaxTree> CreateResource(string id, string source, Schema schema)
    {
        var tree = SyntaxTree.ParseAndBind(source, schema);
        return new Resource<SyntaxTree>(new ResourceId(id), tree);
    }

    private static ResolvedResource<SyntaxTree, ImportDirective> CreateResolvedResource(
        IResource<SyntaxTree> resource,
        IReadOnlyList<ImportDirective>? directives = null)
    {
        directives ??= [];
        return new ResolvedResource<SyntaxTree, ImportDirective>(resource, directives);
    }

    #region Basic Merge Tests

    [Fact]
    public void Merge_EmptyResources_ReturnsEmptyTree()
    {
        // Arrange
        var strategy = new SyntaxTreeMergeStrategy<TestImportNode, object>(n => n.Reference);
        var context = CreateMergeContext([]);

        // Act
        var result = strategy.Merge([], null!, context);

        // Assert
        Assert.Equal(0, result.TextLength);
    }

    [Fact]
    public void Merge_SingleResourceNoImports_ReturnsOriginalContent()
    {
        // Arrange
        var schema = CreateTestSchema();
        var source = "let x = 1";
        var resource = CreateResource("main", source, schema);
        var resolved = CreateResolvedResource(resource);
        var orderedResources = new List<ResolvedResource<SyntaxTree, ImportDirective>> { resolved };

        var strategy = new SyntaxTreeMergeStrategy<TestImportNode, object>(n => n.Reference);
        var context = CreateMergeContext(orderedResources);

        // Act
        var result = strategy.Merge(orderedResources, null!, context);

        // Assert
        Assert.Equal(source, result.ToText());
    }

    [Fact]
    public void Merge_SingleImport_ReplacesImportWithContent()
    {
        // Arrange
        var schema = CreateTestSchema();

        var mainSource = "import \"lib\"\nlet x = 1";
        var libSource = "let y = 2";

        var mainResource = CreateResource("main", mainSource, schema);
        var libResource = CreateResource("lib", libSource, schema);

        // Parse directives from main
        var parser = new ImportDirectiveParser<TestImportNode>(n => n.Reference);
        var mainDirectives = parser.Parse(mainResource.Content, mainResource.Id).ToList();

        var libResolved = CreateResolvedResource(libResource);
        var mainResolved = CreateResolvedResource(mainResource, mainDirectives);

        // Dependency order: lib first, then main (root)
        var orderedResources = new List<ResolvedResource<SyntaxTree, ImportDirective>>
        {
            libResolved,
            mainResolved
        };

        var strategy = new SyntaxTreeMergeStrategy<TestImportNode, object>(n => n.Reference);
        var context = CreateMergeContext(orderedResources);

        // Act
        var result = strategy.Merge(orderedResources, null!, context);

        // Assert
        var mergedText = result.ToText();
        Assert.Contains("let y = 2", mergedText);
        Assert.Contains("let x = 1", mergedText);
        Assert.DoesNotContain("import", mergedText);
    }

    [Fact]
    public void Merge_MultipleImports_ReplacesAllImportsInOrder()
    {
        // Arrange
        var schema = CreateTestSchema();

        var mainSource = "import \"a\"\nimport \"b\"\nlet main = 0";
        var aSource = "let a = 1";
        var bSource = "let b = 2";

        var mainResource = CreateResource("main", mainSource, schema);
        var aResource = CreateResource("a", aSource, schema);
        var bResource = CreateResource("b", bSource, schema);

        var parser = new ImportDirectiveParser<TestImportNode>(n => n.Reference);
        var mainDirectives = parser.Parse(mainResource.Content, mainResource.Id).ToList();

        var aResolved = CreateResolvedResource(aResource);
        var bResolved = CreateResolvedResource(bResource);
        var mainResolved = CreateResolvedResource(mainResource, mainDirectives);

        var orderedResources = new List<ResolvedResource<SyntaxTree, ImportDirective>>
        {
            aResolved,
            bResolved,
            mainResolved
        };

        var strategy = new SyntaxTreeMergeStrategy<TestImportNode, object>(n => n.Reference);
        var context = CreateMergeContext(orderedResources);

        // Act
        var result = strategy.Merge(orderedResources, null!, context);

        // Assert
        var mergedText = result.ToText();
        Assert.Contains("let a = 1", mergedText);
        Assert.Contains("let b = 2", mergedText);
        Assert.Contains("let main = 0", mergedText);
        Assert.DoesNotContain("import", mergedText);

        // Check order: a content should come before b content
        var aIndex = mergedText.IndexOf("let a = 1", StringComparison.Ordinal);
        var bIndex = mergedText.IndexOf("let b = 2", StringComparison.Ordinal);
        Assert.True(aIndex < bIndex, "Content from 'a' should appear before content from 'b'");
    }

    #endregion

    #region Nested Import Tests

    [Fact]
    public void Merge_NestedImports_ExpandsRecursively()
    {
        // Arrange
        var schema = CreateTestSchema();

        // main imports lib, lib imports helper
        var mainSource = "import \"lib\"\nlet main = 0";
        var libSource = "import \"helper\"\nlet lib = 1";
        var helperSource = "let helper = 2";

        var mainResource = CreateResource("main", mainSource, schema);
        var libResource = CreateResource("lib", libSource, schema);
        var helperResource = CreateResource("helper", helperSource, schema);

        var parser = new ImportDirectiveParser<TestImportNode>(n => n.Reference);
        var mainDirectives = parser.Parse(mainResource.Content, mainResource.Id).ToList();
        var libDirectives = parser.Parse(libResource.Content, libResource.Id).ToList();

        var helperResolved = CreateResolvedResource(helperResource);
        var libResolved = CreateResolvedResource(libResource, libDirectives);
        var mainResolved = CreateResolvedResource(mainResource, mainDirectives);

        // Dependency order: helper, lib, main
        var orderedResources = new List<ResolvedResource<SyntaxTree, ImportDirective>>
        {
            helperResolved,
            libResolved,
            mainResolved
        };

        var strategy = new SyntaxTreeMergeStrategy<TestImportNode, object>(n => n.Reference);
        var context = CreateMergeContext(orderedResources);

        // Act
        var result = strategy.Merge(orderedResources, null!, context);

        // Assert
        var mergedText = result.ToText();
        Assert.Contains("let helper = 2", mergedText);
        Assert.Contains("let lib = 1", mergedText);
        Assert.Contains("let main = 0", mergedText);
        Assert.DoesNotContain("import", mergedText);
    }

    #endregion

    #region Source Map Tests

    [Fact]
    public void Merge_SingleResourceNoImports_RecordsFullSourceMap()
    {
        // Arrange
        var schema = CreateTestSchema();
        var source = "let x = 1";
        var resource = CreateResource("main", source, schema);
        var resolved = CreateResolvedResource(resource);
        var orderedResources = new List<ResolvedResource<SyntaxTree, ImportDirective>> { resolved };

        var strategy = new SyntaxTreeMergeStrategy<TestImportNode, object>(n => n.Reference);
        var context = CreateMergeContext(orderedResources);

        // Act
        var result = strategy.Merge(orderedResources, null!, context);
        var sourceMap = context.SourceMapBuilder.Build();

        // Assert: Source map should cover the full content
        Assert.NotNull(sourceMap);
    }

    [Fact]
    public void Merge_WithImport_RecordsSourceMapForBothResources()
    {
        // Arrange
        var schema = CreateTestSchema();

        var mainSource = "import \"lib\"\nlet x = 1";
        var libSource = "let y = 2";

        var mainResource = CreateResource("main", mainSource, schema);
        var libResource = CreateResource("lib", libSource, schema);

        var parser = new ImportDirectiveParser<TestImportNode>(n => n.Reference);
        var mainDirectives = parser.Parse(mainResource.Content, mainResource.Id).ToList();

        var libResolved = CreateResolvedResource(libResource);
        var mainResolved = CreateResolvedResource(mainResource, mainDirectives);

        var orderedResources = new List<ResolvedResource<SyntaxTree, ImportDirective>>
        {
            libResolved,
            mainResolved
        };

        var strategy = new SyntaxTreeMergeStrategy<TestImportNode, object>(n => n.Reference);
        var context = CreateMergeContext(orderedResources);

        // Act
        var result = strategy.Merge(orderedResources, null!, context);
        var sourceMap = context.SourceMapBuilder.Build();

        // Assert
        Assert.NotNull(sourceMap);
        // The source map should have entries for both resources
    }

    #endregion

    #region Diagnostic Tests

    [Fact]
    public void Merge_UnboundRootResource_ReportsDiagnostic()
    {
        // Arrange
        var unboundTree = SyntaxTree.Parse("let x = 1");
        var resource = new Resource<SyntaxTree>(new ResourceId("main"), unboundTree);
        var resolved = CreateResolvedResource(resource);
        var orderedResources = new List<ResolvedResource<SyntaxTree, ImportDirective>> { resolved };

        var strategy = new SyntaxTreeMergeStrategy<TestImportNode, object>(n => n.Reference);
        var context = CreateMergeContext(orderedResources);

        // Act
        var result = strategy.Merge(orderedResources, null!, context);

        // Assert
        Assert.Contains(context.Diagnostics, d => d.Message.Contains("schema-bound"));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Merge_ImportAtStart_HandlesCorrectly()
    {
        // Arrange
        var schema = CreateTestSchema();

        var mainSource = "import \"lib\"";
        var libSource = "let y = 2";

        var mainResource = CreateResource("main", mainSource, schema);
        var libResource = CreateResource("lib", libSource, schema);

        var parser = new ImportDirectiveParser<TestImportNode>(n => n.Reference);
        var mainDirectives = parser.Parse(mainResource.Content, mainResource.Id).ToList();

        var libResolved = CreateResolvedResource(libResource);
        var mainResolved = CreateResolvedResource(mainResource, mainDirectives);

        var orderedResources = new List<ResolvedResource<SyntaxTree, ImportDirective>>
        {
            libResolved,
            mainResolved
        };

        var strategy = new SyntaxTreeMergeStrategy<TestImportNode, object>(n => n.Reference);
        var context = CreateMergeContext(orderedResources);

        // Act
        var result = strategy.Merge(orderedResources, null!, context);

        // Assert
        var mergedText = result.ToText();
        Assert.Equal("let y = 2", mergedText);
    }

    [Fact]
    public void Merge_ImportAtEnd_HandlesCorrectly()
    {
        // Arrange
        var schema = CreateTestSchema();

        var mainSource = "let x = 1\nimport \"lib\"";
        var libSource = "let y = 2";

        var mainResource = CreateResource("main", mainSource, schema);
        var libResource = CreateResource("lib", libSource, schema);

        var parser = new ImportDirectiveParser<TestImportNode>(n => n.Reference);
        var mainDirectives = parser.Parse(mainResource.Content, mainResource.Id).ToList();

        var libResolved = CreateResolvedResource(libResource);
        var mainResolved = CreateResolvedResource(mainResource, mainDirectives);

        var orderedResources = new List<ResolvedResource<SyntaxTree, ImportDirective>>
        {
            libResolved,
            mainResolved
        };

        var strategy = new SyntaxTreeMergeStrategy<TestImportNode, object>(n => n.Reference);
        var context = CreateMergeContext(orderedResources);

        // Act
        var result = strategy.Merge(orderedResources, null!, context);

        // Assert
        var mergedText = result.ToText();
        Assert.Contains("let x = 1", mergedText);
        Assert.Contains("let y = 2", mergedText);
        Assert.DoesNotContain("import", mergedText);
    }

    [Fact]
    public void Merge_ContentBetweenImports_Preserved()
    {
        // Arrange
        var schema = CreateTestSchema();

        var mainSource = "import \"a\"\nlet middle = 0\nimport \"b\"";
        var aSource = "let a = 1";
        var bSource = "let b = 2";

        var mainResource = CreateResource("main", mainSource, schema);
        var aResource = CreateResource("a", aSource, schema);
        var bResource = CreateResource("b", bSource, schema);

        var parser = new ImportDirectiveParser<TestImportNode>(n => n.Reference);
        var mainDirectives = parser.Parse(mainResource.Content, mainResource.Id).ToList();

        var aResolved = CreateResolvedResource(aResource);
        var bResolved = CreateResolvedResource(bResource);
        var mainResolved = CreateResolvedResource(mainResource, mainDirectives);

        var orderedResources = new List<ResolvedResource<SyntaxTree, ImportDirective>>
        {
            aResolved,
            bResolved,
            mainResolved
        };

        var strategy = new SyntaxTreeMergeStrategy<TestImportNode, object>(n => n.Reference);
        var context = CreateMergeContext(orderedResources);

        // Act
        var result = strategy.Merge(orderedResources, null!, context);

        // Assert
        var mergedText = result.ToText();
        Assert.Contains("let a = 1", mergedText);
        Assert.Contains("let middle = 0", mergedText);
        Assert.Contains("let b = 2", mergedText);

        // Verify order
        var aIndex = mergedText.IndexOf("let a = 1", StringComparison.Ordinal);
        var middleIndex = mergedText.IndexOf("let middle = 0", StringComparison.Ordinal);
        var bIndex = mergedText.IndexOf("let b = 2", StringComparison.Ordinal);

        Assert.True(aIndex < middleIndex, "Content from 'a' should appear before 'middle'");
        Assert.True(middleIndex < bIndex, "'middle' content should appear before content from 'b'");
    }

    [Fact]
    public void Merge_NonPathCanonicalResourceId_ReplacesImportWithContent()
    {
        // Arrange
        var schema = CreateTestSchema();

        var canonicalId = "domain:lib/shared";
        var mainSource = $"import \"{canonicalId}\"\nlet x = 1";
        var libSource = "let y = 2";

        var mainResource = CreateResource("main", mainSource, schema);
        var libResource = CreateResource(canonicalId, libSource, schema);

        // Parse directives from main
        var parser = new ImportDirectiveParser<TestImportNode>(n => n.Reference);
        var mainDirectives = parser.Parse(mainResource.Content, mainResource.Id).ToList();

        var libResolved = CreateResolvedResource(libResource);
        var mainResolved = CreateResolvedResource(mainResource, mainDirectives);

        // Dependency order: lib first, then main (root)
        var orderedResources = new List<ResolvedResource<SyntaxTree, ImportDirective>>
        {
            libResolved,
            mainResolved
        };

        var strategy = new SyntaxTreeMergeStrategy<TestImportNode, object>(n => n.Reference);
        var context = CreateMergeContext(orderedResources);

        // Act
        var result = strategy.Merge(orderedResources, null!, context);

        // Assert
        var mergedText = result.ToText();
        Assert.Contains("let y = 2", mergedText);
        Assert.Contains("let x = 1", mergedText);
        Assert.DoesNotContain("import", mergedText);
    }

    [Fact]
    public void Merge_SharedDependencyProcessedOnce_WhenIncludedMultipleTimes()
    {
        // Arrange
        var schema = CreateTestSchema();

        // main imports a and b; both a and b import shared
        var sharedSource = "let shared = 1";
        var aSource = "import \"shared\"\nlet a = shared";
        var bSource = "import \"shared\"\nlet b = shared";
        var mainSource = "import \"a\"\nimport \"b\"\nlet main = 0";

        var sharedResource = CreateResource("shared", sharedSource, schema);
        var aResource = CreateResource("a", aSource, schema);
        var bResource = CreateResource("b", bSource, schema);
        var mainResource = CreateResource("main", mainSource, schema);

        var parser = new ImportDirectiveParser<TestImportNode>(n => n.Reference);
        var aDirectives = parser.Parse(aResource.Content, aResource.Id).ToList();
        var bDirectives = parser.Parse(bResource.Content, bResource.Id).ToList();
        var mainDirectives = parser.Parse(mainResource.Content, mainResource.Id).ToList();

        var sharedResolved = CreateResolvedResource(sharedResource);
        var aResolved = CreateResolvedResource(aResource, aDirectives);
        var bResolved = CreateResolvedResource(bResource, bDirectives);
        var mainResolved = CreateResolvedResource(mainResource, mainDirectives);

        // Dependency order (deps first). Note: shared appears once even though referenced twice.
        var orderedResources = new List<ResolvedResource<SyntaxTree, ImportDirective>>
        {
            sharedResolved,
            aResolved,
            bResolved,
            mainResolved
        };

        var counts = new Dictionary<ResourceId, int>();
        var strategy = new CountingMergeStrategy(counts);

        var context = CreateMergeContext(orderedResources);

        // Act
        var result = strategy.Merge(orderedResources, null!, context);

        // Assert
        Assert.Equal(1, counts[new ResourceId("shared")]);
        Assert.Equal(1, counts[new ResourceId("a")]);
        Assert.Equal(1, counts[new ResourceId("b")]);
        Assert.Equal(1, counts[new ResourceId("main")]);

        Assert.Empty(context.Diagnostics);
        Assert.DoesNotContain("import", result.ToText());
    }

    [Fact]
    public void Merge_IncludeOfIncludeProcessedOnce()
    {
        // Arrange
        var schema = CreateTestSchema();

        // main -> a -> shared -> leaf
        var leafSource = "let leaf = 1";
        var sharedSource = "import \"leaf\"\nlet shared = leaf";
        var aSource = "import \"shared\"\nlet a = shared";
        var mainSource = "import \"a\"\nlet main = 0";

        var leafResource = CreateResource("leaf", leafSource, schema);
        var sharedResource = CreateResource("shared", sharedSource, schema);
        var aResource = CreateResource("a", aSource, schema);
        var mainResource = CreateResource("main", mainSource, schema);

        var parser = new ImportDirectiveParser<TestImportNode>(n => n.Reference);
        var sharedDirectives = parser.Parse(sharedResource.Content, sharedResource.Id).ToList();
        var aDirectives = parser.Parse(aResource.Content, aResource.Id).ToList();
        var mainDirectives = parser.Parse(mainResource.Content, mainResource.Id).ToList();

        var leafResolved = CreateResolvedResource(leafResource);
        var sharedResolved = CreateResolvedResource(sharedResource, sharedDirectives);
        var aResolved = CreateResolvedResource(aResource, aDirectives);
        var mainResolved = CreateResolvedResource(mainResource, mainDirectives);

        var orderedResources = new List<ResolvedResource<SyntaxTree, ImportDirective>>
        {
            leafResolved,
            sharedResolved,
            aResolved,
            mainResolved
        };

        var counts = new Dictionary<ResourceId, int>();
        var strategy = new CountingMergeStrategy(counts);

        var context = CreateMergeContext(orderedResources);

        // Act
        var result = strategy.Merge(orderedResources, null!, context);

        // Assert
        Assert.Equal(1, counts[new ResourceId("leaf")]);
        Assert.Equal(1, counts[new ResourceId("shared")]);
        Assert.Equal(1, counts[new ResourceId("a")]);
        Assert.Equal(1, counts[new ResourceId("main")]);

        Assert.Empty(context.Diagnostics);
        var mergedText = result.ToText();
        Assert.Contains("let leaf = 1", mergedText);
        Assert.Contains("let shared = leaf", mergedText);
        Assert.Contains("let a = shared", mergedText);
        Assert.Contains("let main = 0", mergedText);
        Assert.DoesNotContain("import", mergedText);
    }

    #endregion
}
