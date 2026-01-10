using TinyAst.Preprocessor.Bridge.Imports;
using TinyPreprocessor.Core;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Tests.Bridge.Imports;

public class ImportDirectiveParserTests
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

    [Fact]
    public void Parse_UnboundTree_Throws()
    {
        // Arrange
        var tree = SyntaxTree.Parse("import \"a\"");
        Assert.False(tree.HasSchema);

        var parser = ImportDirectiveParser<TestImportNode>.Instance;

        // Act
        var act = () => parser.Parse(tree, new ResourceId("main"))
            .ToArray();

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Parse_BoundTree_YieldsDirectivesInDocumentOrderWithAnchoredLocations()
    {
        // Arrange
        var text = "import \"a\"\n  import \"b\"";
        var schema = CreateTestSchema();
        var tree = SyntaxTree.ParseAndBind(text, schema);
        Assert.True(tree.HasSchema);

        var resourceId = new ResourceId("main");
        var parser = ImportDirectiveParser<TestImportNode>.Instance;

        // Expected positions must match TinyAst's trivia-inclusive coordinates.
        // Rather than assuming string indices, we take them from the bound nodes.
        var expectedPositions = tree
            .Select(Query.Syntax<TestImportNode>())
            .OfType<TestImportNode>()
            .OrderBy(n => n.Position)
            .ThenBy(n => n.SiblingIndex)
            .Select(n => n.Position)
            .ToArray();

        // Act
        var directives = parser.Parse(tree, resourceId).ToArray();

        // Assert
        Assert.Equal(2, directives.Length);

        Assert.Equal("a", directives[0].Reference);
        Assert.Equal(expectedPositions[0]..expectedPositions[0], directives[0].Location);
        Assert.Equal(resourceId, directives[0].Resource);

        Assert.Equal("b", directives[1].Reference);
        Assert.Equal(expectedPositions[1]..expectedPositions[1], directives[1].Location);
        Assert.Equal(resourceId, directives[1].Resource);
    }

    [Fact]
    public void Parse_IgnoresMalformedImportNodes_WhenReferenceEmpty()
    {
        // Arrange
        var text = "import \"\"\nimport \"ok\"";
        var schema = CreateTestSchema();
        var tree = SyntaxTree.ParseAndBind(text, schema);

        var parser = ImportDirectiveParser<TestImportNode>.Instance;
        var resourceId = new ResourceId("main");

        // Act
        var directives = parser.Parse(tree, resourceId).ToArray();

        // Assert
        Assert.Single(directives);
        Assert.Equal("ok", directives[0].Reference);
    }
}
