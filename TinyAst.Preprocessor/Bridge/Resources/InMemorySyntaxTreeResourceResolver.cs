using TinyAst.Preprocessor.Bridge.Imports;
using TinyPreprocessor.Core;
using TinyPreprocessor.Diagnostics;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Bridge.Resources;

/// <summary>
/// Resolves <see cref="SyntaxTree"/> resources from an <see cref="InMemorySyntaxTreeResourceStore"/>.
/// </summary>
/// <remarks>
/// This resolver supports path-like reference resolution via <see cref="ResourceIdPathResolver"/>.
/// It can optionally use an <see cref="ImportDirectiveLocationIndex"/> to attach precise locations to
/// resolution failure diagnostics.
/// </remarks>
public sealed class InMemorySyntaxTreeResourceResolver : IResourceResolver<SyntaxTree>
{
    private readonly InMemorySyntaxTreeResourceStore _store;
    private readonly ImportDirectiveLocationIndex? _locationIndex;

    /// <summary>
    /// Creates a resolver backed by an in-memory store.
    /// </summary>
    /// <param name="store">The store containing resources keyed by canonical <see cref="ResourceId"/>.</param>
    /// <param name="locationIndex">
    /// Optional location index for pinning resolution failure diagnostics to the corresponding import site.
    /// </param>
    public InMemorySyntaxTreeResourceResolver(InMemorySyntaxTreeResourceStore store, ImportDirectiveLocationIndex? locationIndex = null)
    {
        _store = store;
        _locationIndex = locationIndex;
    }

    /// <summary>
    /// Resolves <paramref name="reference"/> against <paramref name="context"/> into an existing resource.
    /// </summary>
    /// <param name="reference">The raw reference string.</param>
    /// <param name="context">The requesting resource (if available).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A success result with the resolved resource, or a failure result with a diagnostic.</returns>
    public ValueTask<ResourceResolutionResult<SyntaxTree>> ResolveAsync(
        string reference,
        IResource<SyntaxTree>? context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var referenceKey = reference.Trim();

        var resolvedId = ResourceIdPathResolver.Resolve(referenceKey, context);

        if (_store.TryGet(resolvedId, out var resource))
        {
            return ValueTask.FromResult(ResourceResolutionResult<SyntaxTree>.Success(resource));
        }

        Range? location = null;
        if (context is not null && _locationIndex is not null && _locationIndex.TryDequeue(context.Id, referenceKey, out var loc))
        {
            location = loc;
        }

        var diagnostic = new ResolutionFailedDiagnostic(
            reference,
            "NotFound",
            context?.Id,
            location);

        return ValueTask.FromResult(ResourceResolutionResult<SyntaxTree>.Failure(diagnostic));
    }
}
