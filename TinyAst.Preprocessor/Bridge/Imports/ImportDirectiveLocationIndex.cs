using System.Collections.Concurrent;
using TinyPreprocessor.Core;

namespace TinyAst.Preprocessor.Bridge.Imports;

/// <summary>
/// Stores directive locations keyed by the importing resource and raw reference.
/// </summary>
/// <remarks>
/// This is primarily used by resolvers to retrieve the next location for a given reference so
/// resolution failures can be pinned to the originating import site.
/// </remarks>
public sealed class ImportDirectiveLocationIndex
{
    private readonly ConcurrentDictionary<(ResourceId Resource, string Reference), ConcurrentQueue<Range>> _locations = new();

    private static string NormalizeReferenceKey(string reference) => reference.Trim();

    /// <summary>
    /// Adds a directive location for a specific importing resource and reference.
    /// </summary>
    /// <param name="resource">The importing resource id.</param>
    /// <param name="reference">The raw reference string.</param>
    /// <param name="location">The directive location within the importing resource.</param>
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

    /// <summary>
    /// Attempts to dequeue the next location for a given importing resource and reference.
    /// </summary>
    /// <param name="resource">The importing resource id.</param>
    /// <param name="reference">The raw reference string.</param>
    /// <param name="location">The dequeued location, if available.</param>
    /// <returns><see langword="true"/> if a location was dequeued; otherwise <see langword="false"/>.</returns>
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
