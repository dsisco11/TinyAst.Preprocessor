using TinyAst.Preprocessor.Bridge.Imports;
using TinyPreprocessor.Core;

namespace TinyAst.Preprocessor.Tests.Bridge.Imports;

public class ImportDirectiveModelTests
{
    private readonly ImportDirectiveModel _model = ImportDirectiveModel.Instance;

    #region GetLocation Tests

    [Fact]
    public void GetLocation_ReturnsDirectiveLocation()
    {
        // Arrange: Location anchored at position 42 (zero-length range)
        var position = 42;
        var expectedLocation = position..position;
        var directive = new ImportDirective("some/path", expectedLocation);

        // Act
        var location = _model.GetLocation(directive);

        // Assert
        Assert.Equal(expectedLocation, location);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(12345)]
    public void GetLocation_ReturnsZeroLengthRangeAtNodePosition(int position)
    {
        // Arrange: Simulates Position..Position from TinyAst node coordinates
        var expectedLocation = position..position;
        var directive = new ImportDirective("reference", expectedLocation);

        // Act
        var location = _model.GetLocation(directive);

        // Assert
        Assert.Equal(position, location.Start.Value);
        Assert.Equal(position, location.End.Value);
    }

    #endregion

    #region TryGetReference Tests

    [Fact]
    public void TryGetReference_ValidReference_ReturnsTrue()
    {
        // Arrange
        var expectedRef = "path/to/file.txt";
        var directive = new ImportDirective(expectedRef, 0..0);

        // Act
        var result = _model.TryGetReference(directive, out var reference);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedRef, reference);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void TryGetReference_EmptyOrWhitespaceReference_ReturnsFalse(string? invalidRef)
    {
        // Arrange
        var directive = new ImportDirective(invalidRef!, 0..0);

        // Act
        var result = _model.TryGetReference(directive, out var reference);

        // Assert
        Assert.False(result);
        Assert.Equal(string.Empty, reference);
    }

    [Fact]
    public void TryGetReference_ReferenceWithLeadingTrailingSpaces_ReturnsOriginal()
    {
        // Arrange: Reference with spaces is valid (trimming is not the model's responsibility)
        var refWithSpaces = "  path/to/file  ";
        var directive = new ImportDirective(refWithSpaces, 0..0);

        // Act
        var result = _model.TryGetReference(directive, out var reference);

        // Assert
        Assert.True(result);
        Assert.Equal(refWithSpaces, reference);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Directive_WithResourceId_PreservesResource()
    {
        // Arrange
        var resourceId = new ResourceId("main.txt");
        var directive = new ImportDirective("ref", 10..10, resourceId);

        // Act & Assert
        Assert.Equal(resourceId, directive.Resource);
        Assert.Equal(10..10, _model.GetLocation(directive));
    }

    [Fact]
    public void Directive_DefaultResourceId_IsNull()
    {
        // Arrange
        var directive = new ImportDirective("ref", 0..0);

        // Assert
        Assert.Null(directive.Resource);
    }

    #endregion
}
