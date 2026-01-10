using TinyAst.Preprocessor.Bridge.Imports;
using TinyPreprocessor.Core;
using TinyPreprocessor.Diagnostics;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Bridge.Resources;

public sealed class InMemorySyntaxTreeResourceResolver : IResourceResolver<SyntaxTree>
{
    private readonly InMemorySyntaxTreeResourceStore _store;
    private readonly ImportDirectiveLocationIndex? _locationIndex;

    public InMemorySyntaxTreeResourceResolver(InMemorySyntaxTreeResourceStore store, ImportDirectiveLocationIndex? locationIndex = null)
    {
        _store = store;
        _locationIndex = locationIndex;
    }

    public ValueTask<ResourceResolutionResult<SyntaxTree>> ResolveAsync(
        string reference,
        IResource<SyntaxTree>? context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var resolvedId = ResourceIdPathResolver.Resolve(reference, context);

        if (_store.TryGet(resolvedId, out var resource))
        {
            return ValueTask.FromResult(ResourceResolutionResult<SyntaxTree>.Success(resource));
        }

        Range? location = null;
        if (context is not null && _locationIndex is not null && _locationIndex.TryDequeue(context.Id, reference, out var loc))
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
