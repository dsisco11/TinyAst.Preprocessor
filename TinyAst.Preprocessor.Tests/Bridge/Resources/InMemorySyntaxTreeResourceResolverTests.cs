using TinyAst.Preprocessor.Bridge.Imports;
using TinyAst.Preprocessor.Bridge.Resources;
using TinyPreprocessor.Core;
using TinyPreprocessor.Diagnostics;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Tests.Bridge.Resources;

public class InMemorySyntaxTreeResourceResolverTests
{
    private static Schema CreateTestSchema()
    {
        var importPattern = new PatternBuilder()
            .Ident("import")
            .String()
            .BuildQuery();

        return Schema.Create()
            .DefineSyntax<Bridge.Imports.TestImportNode>(
                "Import",
                b => b.Match(importPattern))
            .Build();
    }

    [Fact]
    public async Task ResolveAsync_Success_ResolvesRelativeToContextIdDirectory()
    {
        // Arrange
        var store = new InMemorySyntaxTreeResourceStore();
        var resolver = new InMemorySyntaxTreeResourceResolver(store);

        var schema = CreateTestSchema();

        var mainId = new ResourceId("root/main");
        var depId = new ResourceId("root/dep");

        var mainTree = SyntaxTree.ParseAndBind("import \"dep\"", schema);
        var depTree = SyntaxTree.ParseAndBind("", schema);

        var main = new Resource<SyntaxTree>(mainId, mainTree);
        var dep = new Resource<SyntaxTree>(depId, depTree);

        store.Add(main);
        store.Add(dep);

        // Act
        var result = await resolver.ResolveAsync("dep", main, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Resource);
        Assert.Equal(depId, result.Resource!.Id);
    }

    [Fact]
    public async Task ResolveAsync_Failure_PinsDiagnosticToDirectiveLocation()
    {
        // Arrange
        var schema = CreateTestSchema();
        var text = "import \"missing\"";

        var tree = SyntaxTree.ParseAndBind(text, schema);
        var mainId = new ResourceId("main");
        var main = new Resource<SyntaxTree>(mainId, tree);

        var parser = new ImportDirectiveParser<Bridge.Imports.TestImportNode>(n => n.Reference);
        var directive = Assert.Single(parser.Parse(tree, mainId));

        var locationIndex = new ImportDirectiveLocationIndex();
        locationIndex.Add(mainId, directive.Reference, directive.Location);

        var store = new InMemorySyntaxTreeResourceStore();
        store.Add(main);

        var resolver = new InMemorySyntaxTreeResourceResolver(store, locationIndex);

        // Act
        var result = await resolver.ResolveAsync(directive.Reference, main, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var diagnostic = Assert.IsType<ResolutionFailedDiagnostic>(result.Error);
        Assert.Equal(directive.Reference, diagnostic.Reference);
        Assert.Equal(mainId, diagnostic.Resource);
        Assert.Equal(directive.Location, diagnostic.Location);
    }
}
