using TinyPreprocessor.Core;

namespace TinyAst.Preprocessor.Bridge.Imports;

/// <summary>
/// Represents an import directive extracted from a syntax tree.
/// </summary>
/// <param name="Reference">The import reference string (e.g., file path, module name).</param>
/// <param name="Location">
/// The location of the directive in absolute character offsets (TinyAst node coordinates).
/// Anchored to the import node's start position as a zero-length range.
/// </param>
/// <param name="Resource">
/// Optional resource identifier for the file containing this directive.
/// Used for diagnostic reporting.
/// </param>
public readonly record struct ImportDirective(
    string Reference,
    Range Location,
    ResourceId? Resource = null);
