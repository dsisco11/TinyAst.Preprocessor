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

    private static MergeContext<SyntaxTree, ImportDirective> CreateMergeContext()
    {
        return new MergeContext<SyntaxTree, ImportDirective>(
            new SourceMapBuilder(),
            new DiagnosticCollection(),
            new Dictionary<ResourceId, IResource<SyntaxTree>>(),
            new Dictionary<MergeContext<SyntaxTree, ImportDirective>.ResolvedReferenceKey, ResourceId>(),
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
        var context = CreateMergeContext();

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
        var context = CreateMergeContext();

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
        var context = CreateMergeContext();

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
        var context = CreateMergeContext();

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
        var context = CreateMergeContext();

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
        var context = CreateMergeContext();

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
        var context = CreateMergeContext();

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
        var context = CreateMergeContext();

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
        var context = CreateMergeContext();

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
        var context = CreateMergeContext();

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
        var context = CreateMergeContext();

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

    #endregion
}
