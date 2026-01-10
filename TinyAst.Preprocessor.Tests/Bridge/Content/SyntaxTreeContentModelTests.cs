using TinyAst.Preprocessor.Bridge.Content;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Tests.Bridge.Content;

public class SyntaxTreeContentModelTests
{
    private readonly SyntaxTreeContentModel _model = SyntaxTreeContentModel.Instance;

    #region GetLength Tests

    [Fact]
    public void GetLength_EmptyTree_ReturnsZero()
    {
        // Arrange
        var tree = SyntaxTree.Empty;

        // Act
        var length = _model.GetLength(tree);

        // Assert
        Assert.Equal(0, length);
    }

    [Theory]
    [InlineData("hello", 5)]
    [InlineData("hello world", 11)]
    [InlineData("  leading spaces", 16)]
    [InlineData("trailing spaces  ", 17)]
    [InlineData("line1\nline2", 11)]
    [InlineData("", 0)]
    public void GetLength_ReturnsTextLength(string source, int expectedLength)
    {
        // Arrange
        var tree = SyntaxTree.Parse(source, Schema.Default);

        // Act
        var length = _model.GetLength(tree);

        // Assert
        Assert.Equal(expectedLength, length);
    }

    [Fact]
    public void GetLength_NullContent_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _model.GetLength(null!));
    }

    [Fact]
    public void GetLength_MatchesTreeTextLength()
    {
        // Arrange
        var source = "function foo() { return 42; }";
        var tree = SyntaxTree.Parse(source, Schema.Default);

        // Act
        var length = _model.GetLength(tree);

        // Assert: GetLength should match SyntaxTree.TextLength exactly
        Assert.Equal(tree.TextLength, length);
    }

    #endregion

    #region Position Consistency Tests

    [Fact]
    public void GetLength_AllNodePositions_WithinBounds()
    {
        // Arrange: Tree with various node types
        var source = "let x = 123; // comment\nlet y = 456;";
        var tree = SyntaxTree.Parse(source, Schema.Default);
        var length = _model.GetLength(tree);

        // Act & Assert: All leaf nodes should have positions within [0, length]
        foreach (var leaf in tree.Leaves)
        {
            Assert.True(leaf.Position >= 0, $"Node position {leaf.Position} should be >= 0");
            Assert.True(leaf.Position <= length, $"Node position {leaf.Position} should be <= {length}");
        }
    }

    [Fact]
    public void GetLength_RootNodeWidth_MatchesLength()
    {
        // Arrange
        var source = "test content with some tokens";
        var tree = SyntaxTree.Parse(source, Schema.Default);

        // Act
        var length = _model.GetLength(tree);

        // Assert: Root node spans full content
        Assert.Equal(tree.Width, length);
    }

    #endregion

    #region Slice Tests - Valid Cases

    [Fact]
    public void Slice_ZeroLength_ReturnsEmptyTree()
    {
        // Arrange
        var source = "hello world";
        var tree = SyntaxTree.Parse(source, Schema.Default);

        // Act
        var sliced = _model.Slice(tree, 0, 0);

        // Assert
        Assert.Equal(0, sliced.TextLength);
        Assert.Equal(string.Empty, sliced.ToText());
    }

    [Fact]
    public void Slice_ZeroLengthAtMiddle_ReturnsEmptyTree()
    {
        // Arrange
        var source = "hello world";
        var tree = SyntaxTree.Parse(source, Schema.Default);

        // Act
        var sliced = _model.Slice(tree, 5, 0);

        // Assert
        Assert.Equal(0, sliced.TextLength);
        Assert.Equal(string.Empty, sliced.ToText());
    }

    [Fact]
    public void Slice_FullContent_ReturnsEquivalentTree()
    {
        // Arrange
        var source = "hello world";
        var tree = SyntaxTree.Parse(source, Schema.Default);

        // Act
        var sliced = _model.Slice(tree, 0, source.Length);

        // Assert
        Assert.Equal(source, sliced.ToText());
    }

    [Theory]
    [InlineData("hello world", 0, 5, "hello")]
    [InlineData("hello world", 6, 5, "world")]
    [InlineData("hello world", 0, 11, "hello world")]
    [InlineData("hello world", 5, 1, " ")]
    [InlineData("abcdefghij", 2, 4, "cdef")]
    public void Slice_ValidRange_ReturnsExpectedText(string source, int start, int length, string expected)
    {
        // Arrange
        var tree = SyntaxTree.Parse(source, Schema.Default);

        // Act
        var sliced = _model.Slice(tree, start, length);

        // Assert
        Assert.Equal(expected, sliced.ToText());
    }

    [Fact]
    public void Slice_AtEnd_ReturnsEmptyTree()
    {
        // Arrange
        var source = "hello";
        var tree = SyntaxTree.Parse(source, Schema.Default);

        // Act
        var sliced = _model.Slice(tree, source.Length, 0);

        // Assert
        Assert.Equal(0, sliced.TextLength);
        Assert.Equal(string.Empty, sliced.ToText());
    }

    #endregion

    #region Slice Tests - Edge Cases

    [Fact]
    public void Slice_EmptyTree_ZeroLength_ReturnsEmptyTree()
    {
        // Arrange
        var tree = SyntaxTree.Empty;

        // Act
        var sliced = _model.Slice(tree, 0, 0);

        // Assert
        Assert.Equal(0, sliced.TextLength);
        Assert.Equal(string.Empty, sliced.ToText());
    }

    [Fact]
    public void Slice_SingleCharacter_ReturnsCorrectSlice()
    {
        // Arrange
        var source = "abcde";
        var tree = SyntaxTree.Parse(source, Schema.Default);

        // Act
        var sliced = _model.Slice(tree, 2, 1);

        // Assert
        Assert.Equal("c", sliced.ToText());
    }

    [Fact]
    public void Slice_WithWhitespace_PreservesWhitespace()
    {
        // Arrange
        var source = "  hello  ";
        var tree = SyntaxTree.Parse(source, Schema.Default);

        // Act: Slice the leading whitespace
        var sliced = _model.Slice(tree, 0, 2);

        // Assert
        Assert.Equal("  ", sliced.ToText());
    }

    [Fact]
    public void Slice_WithNewlines_PreservesNewlines()
    {
        // Arrange
        var source = "line1\nline2\nline3";
        var tree = SyntaxTree.Parse(source, Schema.Default);

        // Act: Slice across newline
        var sliced = _model.Slice(tree, 4, 4);

        // Assert
        Assert.Equal("1\nli", sliced.ToText());
    }

    #endregion

    #region Slice Tests - Argument Validation

    [Fact]
    public void Slice_NullContent_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _model.Slice(null!, 0, 0));
    }

    [Fact]
    public void Slice_NegativeStart_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var tree = SyntaxTree.Parse("hello", Schema.Default);

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => _model.Slice(tree, -1, 1));
        Assert.Equal("start", ex.ParamName);
    }

    [Fact]
    public void Slice_NegativeLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var tree = SyntaxTree.Parse("hello", Schema.Default);

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => _model.Slice(tree, 0, -1));
        Assert.Equal("length", ex.ParamName);
    }

    [Fact]
    public void Slice_StartExceedsLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var tree = SyntaxTree.Parse("hello", Schema.Default);

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => _model.Slice(tree, 6, 1));
        Assert.Equal("length", ex.ParamName);
    }

    [Fact]
    public void Slice_StartPlusLengthExceedsTextLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var tree = SyntaxTree.Parse("hello", Schema.Default);

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => _model.Slice(tree, 3, 5));
        Assert.Equal("length", ex.ParamName);
    }

    [Theory]
    [InlineData(0, 6)]  // Length exceeds by 1
    [InlineData(5, 1)]  // Start at end, length 1
    [InlineData(4, 2)]  // Start + length = 6 > 5
    public void Slice_OutOfBounds_ThrowsArgumentOutOfRangeException(int start, int length)
    {
        // Arrange
        var tree = SyntaxTree.Parse("hello", Schema.Default);  // length = 5

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => _model.Slice(tree, start, length));
    }

    #endregion

    #region Schema Preservation Tests

    [Fact]
    public void Slice_UnboundTree_ReturnsUnboundSlice()
    {
        // Arrange
        var tree = SyntaxTree.Parse("hello world", Schema.Default);

        // Act
        var sliced = _model.Slice(tree, 0, 5);

        // Assert: Sliced tree should have a schema (parsed with Schema.Default)
        Assert.NotNull(sliced);
        Assert.Equal("hello", sliced.ToText());
    }

    #endregion
}
