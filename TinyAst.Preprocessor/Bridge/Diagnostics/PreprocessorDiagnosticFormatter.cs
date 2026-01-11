using System;
using TinyAst.Preprocessor.Bridge.Content;
using TinyPreprocessor.Core;
using TinyPreprocessor.Diagnostics;
using TinyPreprocessor.Text;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Bridge.Diagnostics;

/// <summary>
/// Utilities for rendering diagnostics with human-friendly line/column locations.
/// </summary>
public static class PreprocessorDiagnosticFormatter
{
    /// <summary>
    /// Tries to compute a <c>line:column</c> representation for a diagnostic's <c>(Resource, Location)</c> pair.
    /// </summary>
    /// <remarks>
    /// This works for any <see cref="IPreprocessorDiagnostic"/> that supplies <see cref="IPreprocessorDiagnostic.Resource"/>
    /// and <see cref="IPreprocessorDiagnostic.Location"/>.
    /// </remarks>
    public static bool TryGetLineColumnLocation(
        IPreprocessorDiagnostic diagnostic,
        Func<ResourceId, SyntaxTree?> getContent,
        IContentBoundaryResolverProvider boundaryResolverProvider,
        out string lineColumn)
    {
        lineColumn = string.Empty;

        ArgumentNullException.ThrowIfNull(diagnostic);
        ArgumentNullException.ThrowIfNull(getContent);
        ArgumentNullException.ThrowIfNull(boundaryResolverProvider);

        if (!TryGetResourceAndLocation(diagnostic, out var resourceId, out var location))
        {
            return false;
        }

        var content = getContent(resourceId);
        if (content is null)
        {
            return false;
        }

        if (!boundaryResolverProvider.TryGet<SyntaxTree, LineBoundary>(out var resolver))
        {
            return false;
        }

        return SyntaxTreeLineColumnMapper.TryFormatRange(content, resourceId, location, resolver, out lineColumn);
    }

    /// <summary>
    /// Formats a diagnostic using its existing <see cref="object.ToString"/>, appending a friendly location
    /// suffix when possible.
    /// </summary>
    public static string Format(
        IPreprocessorDiagnostic diagnostic,
        Func<ResourceId, SyntaxTree?> getContent,
        IContentBoundaryResolverProvider boundaryResolverProvider)
    {
        var text = diagnostic.ToString() ?? string.Empty;

        if (!TryGetLineColumnLocation(diagnostic, getContent, boundaryResolverProvider, out var lineColumn))
        {
            return text;
        }

        // Avoid double-reporting if the diagnostic already renders line:column.
        if (text.Contains($"@{lineColumn}", StringComparison.Ordinal))
        {
            return text;
        }

        if (TryGetResource(diagnostic, out var resourceId))
        {
            return $"{text} (at {resourceId}@{lineColumn})";
        }

        return $"{text} (at {lineColumn})";
    }

    /// <summary>
    /// Formats a diagnostic using the same general pattern as C# compiler diagnostics.
    /// </summary>
    /// <remarks>
    /// Pattern:
    /// <c>resource(line,column): severity CODE: message</c>
    /// </remarks>
    public static string FormatCSharp(
        IPreprocessorDiagnostic diagnostic,
        Func<ResourceId, SyntaxTree?> getContent,
        IContentBoundaryResolverProvider boundaryResolverProvider,
        Func<ResourceId, string>? formatResource = null)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        ArgumentNullException.ThrowIfNull(getContent);
        ArgumentNullException.ThrowIfNull(boundaryResolverProvider);

        var severity = ToCSharpSeverityText(diagnostic.Severity);
        var code = string.IsNullOrWhiteSpace(diagnostic.Code) ? "DIAG" : diagnostic.Code;
        var message = ExtractMessage(diagnostic);

        if (diagnostic.Resource is not { } resourceId)
        {
            return $"{severity} {code}: {message}";
        }

        var resourceText = formatResource is null ? resourceId.ToString() : formatResource(resourceId);
        if (formatResource is null)
        {
            resourceText = resourceId.Path;
        }

        if (!TryGetCSharpLocation(diagnostic, getContent, boundaryResolverProvider, out var locationText))
        {
            return $"{resourceText}: {severity} {code}: {message}";
        }

        return $"{resourceText}({locationText}): {severity} {code}: {message}";
    }

    private static bool TryGetResourceAndLocation(IPreprocessorDiagnostic diagnostic, out ResourceId resourceId, out Range location)
    {
        resourceId = default;
        location = default;

        if (!TryGetResource(diagnostic, out resourceId))
        {
            return false;
        }

        if (!TryGetLocation(diagnostic, out location))
        {
            return false;
        }

        return true;
    }

    private static bool TryGetCSharpLocation(
        IPreprocessorDiagnostic diagnostic,
        Func<ResourceId, SyntaxTree?> getContent,
        IContentBoundaryResolverProvider boundaryResolverProvider,
        out string locationText)
    {
        locationText = string.Empty;

        if (!TryGetResourceAndLocation(diagnostic, out var resourceId, out var location))
        {
            return false;
        }

        var content = getContent(resourceId);
        if (content is null)
        {
            return false;
        }

        if (!boundaryResolverProvider.TryGet<SyntaxTree, LineBoundary>(out var resolver))
        {
            return false;
        }

        return SyntaxTreeLineColumnMapper.TryFormatRangeCSharp(content, resourceId, location, resolver, out locationText);
    }

    private static string ToCSharpSeverityText(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "error",
        DiagnosticSeverity.Warning => "warning",
        DiagnosticSeverity.Info => "info",
        _ => severity.ToString().ToLowerInvariant(),
    };

    private static string ExtractMessage(IPreprocessorDiagnostic diagnostic)
    {
        // Prefer a direct Message property when implemented by the diagnostic type.
        // If not present, fall back to ToString() (best-effort).
        var messageProp = diagnostic.GetType().GetProperty("Message");
        if (messageProp?.GetValue(diagnostic) is string s && !string.IsNullOrWhiteSpace(s))
        {
            return s;
        }

        return diagnostic.ToString() ?? string.Empty;
    }

    private static bool TryGetResource(IPreprocessorDiagnostic diagnostic, out ResourceId resourceId)
    {
        if (diagnostic.Resource is not { } id)
        {
            resourceId = default;
            return false;
        }

        resourceId = id;
        return true;
    }

    private static bool TryGetLocation(IPreprocessorDiagnostic diagnostic, out Range location)
    {
        if (diagnostic.Location is not { } loc)
        {
            location = default;
            return false;
        }

        location = loc;
        return true;
    }
}
