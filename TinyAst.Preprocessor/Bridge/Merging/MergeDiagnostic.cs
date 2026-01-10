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
    /// <inheritdoc/>
    public DiagnosticSeverity Severity => DiagnosticSeverity.Error;

    /// <inheritdoc/>
    public string Code => "MERGE001";

    /// <inheritdoc/>
    public override string ToString() =>
        Location.HasValue && Resource.HasValue
            ? $"[{Code}] {Resource}: {Message} at {Location.Value}"
            : $"[{Code}] {Resource}: {Message}";
}
