using System.Collections.Concurrent;
using TinyPreprocessor.Core;

namespace TinyAst.Preprocessor.Bridge.Imports;

public sealed class ImportDirectiveLocationIndex
{
    private readonly ConcurrentDictionary<(ResourceId Resource, string Reference), ConcurrentQueue<Range>> _locations = new();

    private static string NormalizeReferenceKey(string reference) => reference.Trim();

    public void Add(ResourceId resource, string reference, Range location)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return;
        }

        var key = NormalizeReferenceKey(reference);
        var queue = _locations.GetOrAdd((resource, key), _ => new ConcurrentQueue<Range>());
        queue.Enqueue(location);
    }

    public bool TryDequeue(ResourceId resource, string reference, out Range location)
    {
        location = default;

        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        var key = NormalizeReferenceKey(reference);
        if (!_locations.TryGetValue((resource, key), out var queue))
        {
            return false;
        }

        return queue.TryDequeue(out location);
    }
}
