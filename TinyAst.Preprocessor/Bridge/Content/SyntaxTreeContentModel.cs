using TinyPreprocessor.Core;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Bridge.Content;

/// <summary>
/// Implements <see cref="IContentModel{TContent}"/> for <see cref="SyntaxTree"/>.
/// </summary>
/// <remarks>
/// <para>
/// Length and offset semantics are based on TinyAst's node coordinate space:
/// absolute character offsets including trivia.
/// </para>
/// <para>
/// <see cref="GetLength"/> returns the total text length of the tree (<see cref="SyntaxTree.TextLength"/>).
/// </para>
/// <para>
/// <see cref="Slice"/> returns a new <see cref="SyntaxTree"/> by re-parsing the
/// sliced text segment. This preserves the semantic contract but note that the
/// resulting tree is a standalone parse, not a subtree of the original.
/// </para>
/// </remarks>
public sealed class SyntaxTreeContentModel : IContentModel<SyntaxTree>
{
    /// <summary>
    /// Gets the singleton instance of <see cref="SyntaxTreeContentModel"/>.
    /// </summary>
    public static SyntaxTreeContentModel Instance { get; } = new();

    private SyntaxTreeContentModel() { }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns the total text length of the syntax tree in absolute character offsets.
    /// This matches the TinyAst node coordinate space where <c>0 &lt;= node.Position &lt;= GetLength(tree)</c>.
    /// </remarks>
    public int GetLength(SyntaxTree content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return content.TextLength;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// Returns a new <see cref="SyntaxTree"/> containing the text from
    /// <paramref name="start"/> to <paramref name="start"/> + <paramref name="length"/>.
    /// </para>
    /// <para>
    /// The resulting tree is created by extracting the source text slice and
    /// re-parsing it using the original tree's schema (if present).
    /// </para>
    /// <para>
    /// For zero-length slices, returns <see cref="SyntaxTree.Empty"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="start"/> or <paramref name="length"/> is negative, or
    /// <paramref name="start"/> + <paramref name="length"/> exceeds the text length.
    /// </exception>
    public SyntaxTree Slice(SyntaxTree content, int start, int length)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (start < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(start), start, "Start must be non-negative.");
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "Length must be non-negative.");
        }

        var textLength = content.TextLength;
        if (start + length > textLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                length,
                $"Start ({start}) + length ({length}) exceeds text length ({textLength}).");
        }

        if (length == 0)
        {
            return SyntaxTree.Empty;
        }

        var sourceText = content.ToText();
        var slicedText = sourceText.Substring(start, length);

        // Re-parse with the same schema if present
        if (content.HasSchema)
        {
            return SyntaxTree.ParseAndBind(slicedText, content.Schema!);
        }

        // For unbound trees, parse with default schema
        return SyntaxTree.Parse(slicedText, Schema.Default);
    }
}
