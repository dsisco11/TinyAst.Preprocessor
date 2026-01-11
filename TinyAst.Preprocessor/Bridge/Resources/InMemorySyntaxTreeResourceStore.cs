using System.Collections.Concurrent;
using TinyPreprocessor.Core;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Bridge.Resources;

/// <summary>
/// In-memory store for <see cref="SyntaxTree"/> resources keyed by <see cref="ResourceId"/>.
/// </summary>
public sealed class InMemorySyntaxTreeResourceStore
{
    private readonly ConcurrentDictionary<ResourceId, IResource<SyntaxTree>> _resources = new();

    /// <summary>
    /// Adds or replaces a resource in the store.
    /// </summary>
    /// <param name="resource">The resource to add.</param>
    public void Add(IResource<SyntaxTree> resource)
    {
        _resources[resource.Id] = resource;
    }

    /// <summary>
    /// Attempts to get a resource by id.
    /// </summary>
    /// <param name="id">The resource id.</param>
    /// <param name="resource">The resolved resource, if found.</param>
    /// <returns><see langword="true"/> if found; otherwise <see langword="false"/>.</returns>
    public bool TryGet(ResourceId id, out IResource<SyntaxTree> resource)
    {
        return _resources.TryGetValue(id, out resource!);
    }
}
