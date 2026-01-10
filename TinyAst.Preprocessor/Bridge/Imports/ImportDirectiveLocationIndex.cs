using System.Collections.Concurrent;
using TinyPreprocessor.Core;

namespace TinyAst.Preprocessor.Bridge.Imports;

public sealed class ImportDirectiveLocationIndex
{
    private readonly ConcurrentDictionary<(ResourceId Resource, string Reference), ConcurrentQueue<Range>> _locations = new();

    public void Add(ResourceId resource, string reference, Range location)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return;
        }

        var queue = _locations.GetOrAdd((resource, reference), _ => new ConcurrentQueue<Range>());
        queue.Enqueue(location);
    }

    public bool TryDequeue(ResourceId resource, string reference, out Range location)
    {
        location = default;

        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        if (!_locations.TryGetValue((resource, reference), out var queue))
        {
            return false;
        }

        return queue.TryDequeue(out location);
    }
}
