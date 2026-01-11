using TinyAst.Preprocessor.Bridge.Content;
using TinyPreprocessor.Core;
using TinyPreprocessor.SourceMaps;
using TinyPreprocessor.Text;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Tests.Bridge.Content;

public class SyntaxTreeContentBoundaryResolverTests
{
    [Fact]
    public void ResolveOffsets_LineBoundary_ReturnsExpectedLineStarts()
    {
        var schema = Schema.Default;
        var text = "a\nb\nc";
        var tree = SyntaxTree.ParseAndBind(text, schema);

        var resolver = new SyntaxTreeLineBoundaryResolver();
        var offsets = resolver.ResolveOffsets(tree, new ResourceId("main"), startOffset: 0, endOffset: tree.TextLength);

        Assert.Equal([2, 4], offsets);
    }

    [Fact]
    public void ResolveOriginalBoundaryLocation_ComputesBoundaryIndex()
    {
        var schema = Schema.Default;
        var text = "a\nb\nc";
        var tree = SyntaxTree.ParseAndBind(text, schema);
        var resourceId = new ResourceId("main");

        var builder = new SourceMapBuilder();
        builder.AddOffsetSegment(resourceId, generatedStartOffset: 0, originalStartOffset: 0, length: tree.TextLength);
        var sourceMap = builder.Build();

        var resolver = new SyntaxTreeLineBoundaryResolver();

        // Offset 3 is within the second line (0-based), so boundary index should be 1.
        var loc = sourceMap.ResolveOriginalBoundaryLocation<SyntaxTree, LineBoundary>(
            3,
            id => tree,
            resolver);

        Assert.NotNull(loc);
        Assert.Equal(resourceId, loc!.Resource);
        Assert.Equal(3, loc.OriginalOffset);
        Assert.Equal(1, loc.BoundaryIndex);
    }

    [Fact]
    public void TryFormatRange_MapsOffsetToLineColumn()
    {
        var schema = Schema.Default;
        var text = "a\nb\nc";
        var tree = SyntaxTree.ParseAndBind(text, schema);

        var resolver = new SyntaxTreeLineBoundaryResolver();

        // Offset 3 is on line 2 (1-based), column 2 (1-based).
        var ok = SyntaxTreeLineColumnMapper.TryFormatRange(
            tree,
            new ResourceId("main"),
            3..3,
            resolver,
            out var formatted);

        Assert.True(ok);
        Assert.Equal("2:2", formatted);
    }
}
