using System;
using System.Linq;
using TinyPreprocessor.Core;
using TinyPreprocessor.Text;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Bridge.Content;

/// <summary>
/// Converts absolute character offsets into 1-based line/column coordinates using <see cref="IContentBoundaryResolver{TContent,TBoundary}"/>.
/// </summary>
public static class SyntaxTreeLineColumnMapper
{
    /// <summary>
    /// Attempts to map a <see cref="Range"/> to 1-based (line, column) coordinates.
    /// </summary>
    public static bool TryGetLineColumnRange(
        SyntaxTree content,
        ResourceId resourceId,
        Range location,
        IContentBoundaryResolver<SyntaxTree, LineBoundary> resolver,
        out LineColumn start,
        out LineColumn end)
    {
        start = default;
        end = default;

        if (!TryGetOffset(location.Start, content.TextLength, out var startOffset))
        {
            return false;
        }

        if (!TryGetOffset(location.End, content.TextLength, out var endOffset))
        {
            return false;
        }

        if (!TryMapOffset(content, resourceId, startOffset, resolver, out start))
        {
            return false;
        }

        if (!TryMapOffset(content, resourceId, endOffset, resolver, out end))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Attempts to format a <see cref="Range"/> as a <c>line:column</c> or <c>line:column-line:column</c> string.
    /// </summary>
    public static bool TryFormatRange(
        SyntaxTree content,
        ResourceId resourceId,
        Range location,
        IContentBoundaryResolver<SyntaxTree, LineBoundary> resolver,
        out string formatted)
    {
        formatted = string.Empty;

        if (!TryGetLineColumnRange(content, resourceId, location, resolver, out var start, out var end))
        {
            return false;
        }

        formatted = start.Equals(end)
            ? $"{start.Line}:{start.Column}"
            : $"{start.Line}:{start.Column}-{end.Line}:{end.Column}";

        return true;
    }

    /// <summary>
    /// Attempts to format a <see cref="Range"/> as C# compiler-style coordinates:
    /// <c>line,column</c> or <c>line,column,endLine,endColumn</c>.
    /// </summary>
    public static bool TryFormatRangeCSharp(
        SyntaxTree content,
        ResourceId resourceId,
        Range location,
        IContentBoundaryResolver<SyntaxTree, LineBoundary> resolver,
        out string formatted)
    {
        formatted = string.Empty;

        if (!TryGetLineColumnRange(content, resourceId, location, resolver, out var start, out var end))
        {
            return false;
        }

        formatted = start.Equals(end)
            ? $"{start.Line},{start.Column}"
            : $"{start.Line},{start.Column},{end.Line},{end.Column}";

        return true;
    }

    private static bool TryMapOffset(
        SyntaxTree content,
        ResourceId resourceId,
        int offset,
        IContentBoundaryResolver<SyntaxTree, LineBoundary> resolver,
        out LineColumn location)
    {
        location = default;

        if (offset < 0)
        {
            return false;
        }

        // Clamp to [0, TextLength].
        offset = Math.Min(offset, content.TextLength);

        var lineStarts = resolver.ResolveOffsets(content, resourceId, startOffset: 0, endOffset: content.TextLength).ToArray();

        // lineStarts: ordered start offsets of lines AFTER the first.
        // Count how many of those are <= offset.
        var idx = Array.BinarySearch(lineStarts, offset);
        var boundaryCount = idx >= 0 ? idx + 1 : ~idx;

        var line = boundaryCount + 1;
        var lineStartOffset = boundaryCount == 0 ? 0 : lineStarts[boundaryCount - 1];
        var column = (offset - lineStartOffset) + 1;

        location = new LineColumn(line, column);
        return true;
    }

    private static bool TryGetOffset(Index index, int length, out int offset)
    {
        offset = 0;

        if (index.Value < 0)
        {
            return false;
        }

        offset = index.IsFromEnd
            ? length - index.Value
            : index.Value;

        return offset >= 0;
    }
}

/// <summary>
/// 1-based line/column coordinate.
/// </summary>
public readonly record struct LineColumn(int Line, int Column);
