using TinyPreprocessor.Core;

namespace TinyAst.Preprocessor.Bridge.Imports;

/// <summary>
/// Implements <see cref="IDirectiveModel{TDirective}"/> for <see cref="ImportDirective"/>.
/// </summary>
/// <remarks>
/// <para>
/// Location policy: Returns the directive's location as-is (a zero-length range 
/// anchored to the import node's absolute start position).
/// </para>
/// <para>
/// Reference policy: Empty or whitespace references are treated as non-dependencies
/// (returns <see langword="false"/> from <see cref="TryGetReference"/>).
/// </para>
/// </remarks>
public sealed class ImportDirectiveModel : IDirectiveModel<ImportDirective>
{
    /// <summary>
    /// Gets the singleton instance of <see cref="ImportDirectiveModel"/>.
    /// </summary>
    public static ImportDirectiveModel Instance { get; } = new();

    private ImportDirectiveModel() { }

    /// <inheritdoc/>
    public Range GetLocation(ImportDirective directive) => directive.Location;

    /// <inheritdoc/>
    /// <remarks>
    /// Returns <see langword="false"/> if the reference is null, empty, or whitespace.
    /// This causes the directive to be treated as a non-dependency by the preprocessor.
    /// </remarks>
    public bool TryGetReference(ImportDirective directive, out string reference)
    {
        if (string.IsNullOrWhiteSpace(directive.Reference))
        {
            reference = string.Empty;
            return false;
        }

        reference = directive.Reference;
        return true;
    }
}
