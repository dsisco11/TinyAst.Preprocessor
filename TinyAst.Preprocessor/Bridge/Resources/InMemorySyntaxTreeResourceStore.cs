using System.Collections.Concurrent;
using TinyPreprocessor.Core;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Bridge.Resources;

public sealed class InMemorySyntaxTreeResourceStore
{
    private readonly ConcurrentDictionary<ResourceId, IResource<SyntaxTree>> _resources = new();

    public void Add(IResource<SyntaxTree> resource)
    {
        _resources[resource.Id] = resource;
    }

    public bool TryGet(ResourceId id, out IResource<SyntaxTree> resource)
    {
        return _resources.TryGetValue(id, out resource!);
    }
}
