using TinyPreprocessor.Core;
using TinyPreprocessor.Text;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Bridge.Content;

/// <summary>
/// Resolves <see cref="LineBoundary"/> offsets for a <see cref="SyntaxTree"/>.
/// </summary>
/// <remarks>
/// Offsets are absolute character offsets in TinyAst node coordinate space (trivia-inclusive).
/// This implementation uses TinyAst's trivia/query system to find the node that follows each newline.
/// </remarks>
public sealed class SyntaxTreeLineBoundaryResolver : IContentBoundaryResolver<SyntaxTree, LineBoundary>
{
    /// <summary>
    /// Returns line boundary offsets within the half-open range <c>[startOffset, endOffset)</c>.
    /// </summary>
    /// <param name="content">The content instance.</param>
    /// <param name="resourceId">The resource identity for the content (not used for line boundary computation).</param>
    /// <param name="startOffset">Inclusive start offset.</param>
    /// <param name="endOffset">Exclusive end offset.</param>
    /// <returns>An ordered sequence of boundary offsets.</returns>
    public IEnumerable<int> ResolveOffsets(
        SyntaxTree content,
        ResourceId resourceId,
        int startOffset,
        int endOffset)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (startOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startOffset), startOffset, "Start offset must be non-negative.");
        }

        if (endOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(endOffset), endOffset, "End offset must be non-negative.");
        }

        if (endOffset < startOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(endOffset), endOffset, "End offset must be >= start offset.");
        }

        // Half-open range: [startOffset, endOffset)
        // Convention (TinyPreprocessor.Text.LineBoundary): offsets represent the start offsets of lines AFTER the first line.
        var contentLength = content.TextLength;
        if (startOffset >= contentLength || startOffset == endOffset)
        {
            return [];
        }

        // Clamp endOffset to content length.
        endOffset = Math.Min(endOffset, contentLength);

        // Query.Newline yields the syntax node that FOLLOWS a newline (schema-independent via TinyAst trivia).
        // That node's Position is the start offset of the next line.
        return content
            .Select(Query.Newline)
            .OfType<SyntaxNode>()
            .Select(static n => n.Position)
            .Where(o => o >= startOffset && o < endOffset);
    }
}
