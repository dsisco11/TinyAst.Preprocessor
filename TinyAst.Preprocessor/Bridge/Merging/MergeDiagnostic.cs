using TinyPreprocessor.Core;
using TinyPreprocessor.Diagnostics;

namespace TinyAst.Preprocessor.Bridge.Merging;

/// <summary>
/// Diagnostic reported during merge operations.
/// </summary>
/// <param name="Resource">The resource where the diagnostic occurred.</param>
/// <param name="Location">Optional location within the resource.</param>
/// <param name="Message">Description of the issue.</param>
public sealed record MergeDiagnostic(
    ResourceId? Resource,
    Range? Location,
    string Message) : IPreprocessorDiagnostic
{
    /// <summary>
    /// Optional pre-formatted location text (e.g. <c>line:column</c>), typically computed using a content boundary resolver.
    /// </summary>
    public string? LineColumnLocation { get; init; }

    /// <inheritdoc/>
    public DiagnosticSeverity Severity => DiagnosticSeverity.Error;

    /// <inheritdoc/>
    public string Code => "MERGE001";

    /// <inheritdoc/>
    public override string ToString() =>
        Resource.HasValue
            ? LineColumnLocation is not null
                ? $"[{Code}]<{Resource}@{LineColumnLocation}>: {Message}"
                : Location.HasValue
                    ? $"[{Code}]<{Resource}@{Location.Value}>: {Message}"
                    : $"[{Code}]<{Resource}>: {Message}"
            : $"[{Code}]<{Resource}>: {Message}";
}
