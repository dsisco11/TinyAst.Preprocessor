using TinyAst.Preprocessor.Bridge.Imports;
using TinyAst.Preprocessor.Bridge.Content;
using TinyPreprocessor.Core;
using TinyPreprocessor.Merging;
using TinyPreprocessor.Text;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Bridge.Merging;

/// <summary>
/// Implements <see cref="IMergeStrategy{TContent, TDirective, TContext}"/> for <see cref="SyntaxTree"/>
/// by replacing import nodes with the content from resolved resources using <see cref="SyntaxEditor"/>.
/// </summary>
/// <typeparam name="TImportNode">
/// The downstream import node type.
/// </typeparam>
/// <typeparam name="TContext">User-provided context type.</typeparam>
/// <remarks>
/// <para>
/// The merge strategy processes resources in dependency order (dependencies first),
/// replacing each import node with the child nodes from the resolved resource's tree.
/// </para>
/// <para>
/// Splicing semantics: The root's children from each resolved resource are inserted
/// in place of the import node. Import nodes are removed via <see cref="SyntaxEditor"/>.
/// </para>
/// <para>
/// Source map: Offset segments are recorded for each resource's contribution to the
/// merged output, enabling source location mapping.
/// </para>
/// </remarks>
public class SyntaxTreeMergeStrategy<TImportNode, TContext> : IMergeStrategy<SyntaxTree, ImportDirective, TContext>
    where TImportNode : SyntaxNode
{
    private readonly Func<TImportNode, string?> _getReference;
    private static readonly IContentBoundaryResolver<SyntaxTree, LineBoundary> LineBoundaryResolver = new SyntaxTreeLineBoundaryResolver();

    private readonly record struct SourceSegment(ResourceId ResourceId, int OriginalStartOffset, int Length);

    private static SyntaxToken? GetFirstToken(SyntaxNode node)
    {
        var current = node;
        while (current is not SyntaxToken)
        {
            SyntaxNode? firstChild = null;
            for (var i = 0; i < current.SlotCount; i++)
            {
                firstChild = current.GetChild(i);
                if (firstChild != null)
                    break;
            }

            if (firstChild == null)
                return null;

            current = firstChild;
        }

        return (SyntaxToken)current;
    }

    private static SyntaxToken? GetLastToken(SyntaxNode node)
    {
        var current = node;
        while (current is not SyntaxToken)
        {
            SyntaxNode? lastChild = null;
            for (var i = current.SlotCount - 1; i >= 0; i--)
            {
                lastChild = current.GetChild(i);
                if (lastChild != null)
                    break;
            }

            if (lastChild == null)
                return null;

            current = lastChild;
        }

        return (SyntaxToken)current;
    }

    private static bool TryGetImportSpliceBounds(SyntaxNode importNode, out int prefixEnd, out int suffixStart, out int fullEnd)
    {
        // Replace(...) transfers only the leading trivia of the first leaf and trailing trivia of the last leaf.
        // So the "removed" region in the final output is the import directive's *text* plus any internal trivia,
        // but not the outer trivia. We model that by splicing on the first/last token text boundaries.
        var first = GetFirstToken(importNode);
        var last = GetLastToken(importNode);

        if (first == null || last == null)
        {
            prefixEnd = 0;
            suffixStart = 0;
            fullEnd = 0;
            return false;
        }

        prefixEnd = first.TextPosition;
        suffixStart = last.TextEndPosition;
        fullEnd = importNode.EndPosition;

        if (prefixEnd < importNode.Position)
            return false;
        if (suffixStart < prefixEnd)
            return false;
        if (suffixStart > fullEnd)
            return false;

        return true;
    }

    /// <summary>
    /// Creates a merge strategy using a delegate that extracts the import reference from <typeparamref name="TImportNode"/>.
    /// </summary>
    /// <param name="getReference">
    /// Delegate that returns a reference string for an import node. Returning null/empty/whitespace causes the node to be ignored.
    /// </param>
    public SyntaxTreeMergeStrategy(Func<TImportNode, string?> getReference)
    {
        _getReference = getReference ?? throw new ArgumentNullException(nameof(getReference));
    }

    /// <summary>
    /// Hook invoked once for each resource processed by the merge strategy.
    /// </summary>
    /// <remarks>
    /// Intended for observability/testing; the default implementation is a no-op.
    /// </remarks>
    /// <param name="resourceId">The id of the resource being processed.</param>
    protected virtual void OnProcessResource(ResourceId resourceId)
    {
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// Merges resources by:
    /// 1. Processing resources in dependency order (dependencies first)
    /// 2. For each import directive, replacing the import node with resolved content nodes
    /// 3. Recording source map segments for offset mapping
    /// </para>
    /// </remarks>
    public SyntaxTree Merge(
        IReadOnlyList<ResolvedResource<SyntaxTree, ImportDirective>> orderedResources,
        TContext userContext,
        MergeContext<SyntaxTree, ImportDirective> context)
    {
        ArgumentNullException.ThrowIfNull(orderedResources);
        ArgumentNullException.ThrowIfNull(context);

        if (orderedResources.Count == 0)
        {
            return SyntaxTree.Empty;
        }

        // The root resource is the last one in dependency order
        var rootResource = orderedResources[^1];
        var rootContent = rootResource.Content;

        if (!rootContent.HasSchema)
        {
            context.Diagnostics.Add(
                new MergeDiagnostic(
                    rootResource.Id,
                    null,
                    "Root resource must be schema-bound for merge."));
            return rootContent;
        }

        // Build a lookup of processed trees by resource ID.
        // Process in dependency order so nested imports are already expanded.
        var processedTrees = new Dictionary<ResourceId, SyntaxTree>();

        // Build flattened source segments for each processed resource (dependencies first).
        // The root resource's segments become the final merged output mapping.
        var processedSegments = new Dictionary<ResourceId, IReadOnlyList<SourceSegment>>();

        foreach (var resource in orderedResources)
        {
            var processedTree = ProcessResource(resource, processedTrees, processedSegments, context, out var segmentsForResource);
            processedTrees[resource.Id] = processedTree;

            // Validate the segment length against the actual processed output. If it doesn't match,
            // fall back to a single segment mapping the whole processed output to this resource.
            var expectedLength = 0;
            for (var i = 0; i < segmentsForResource.Count; i++)
            {
                expectedLength += segmentsForResource[i].Length;
            }

            if (expectedLength == processedTree.TextLength)
            {
                processedSegments[resource.Id] = segmentsForResource;
            }
            else
            {
                processedSegments[resource.Id] = [new SourceSegment(resource.Id, OriginalStartOffset: 0, processedTree.TextLength)];
            }
        }

        var mergedTree = processedTrees[rootResource.Id];

        // Build source map for the merged result using the root's flattened segments.
        BuildSourceMapFromSegments(mergedTree, rootResource.Id, processedSegments, context);

        return mergedTree;
    }

    private SyntaxTree ProcessResource(
        ResolvedResource<SyntaxTree, ImportDirective> resource,
        Dictionary<ResourceId, SyntaxTree> processedTrees,
        IReadOnlyDictionary<ResourceId, IReadOnlyList<SourceSegment>> processedSegments,
        MergeContext<SyntaxTree, ImportDirective> context,
        out IReadOnlyList<SourceSegment> segmentsForResource)
    {
        OnProcessResource(resource.Id);

        var content = resource.Content;

        if (!content.HasSchema)
        {
            // Cannot process unbound trees
            segmentsForResource = [new SourceSegment(resource.Id, OriginalStartOffset: 0, content.TextLength)];
            return content;
        }

        // Find import nodes in this tree in the same order as the directive parser:
        // document order + filtered to only nodes with valid references.
        // This ordering is required because TinyPreprocessor v0.4 keys resolved references
        // by (requestingResourceId, directiveIndex).
        var importNodesByDirectiveIndex = content
            .Select(Query.Syntax<TImportNode>())
            .OfType<TImportNode>()
            .OrderBy(n => n.Position)
            .ThenBy(n => n.SiblingIndex)
            .Where(n => !string.IsNullOrWhiteSpace(_getReference(n)))
            .ToList();

        if (importNodesByDirectiveIndex.Count == 0)
        {
            segmentsForResource = [new SourceSegment(resource.Id, OriginalStartOffset: 0, content.TextLength)];
            return content;
        }

        // Build source segments for this processed resource by splicing at import node spans.
        // This avoids a second pass over the tree just to compute the source map.
        var outputSegments = new List<SourceSegment>(capacity: importNodesByDirectiveIndex.Count * 2 + 1);
        var cursor = 0;

        for (var directiveIndex = 0; directiveIndex < importNodesByDirectiveIndex.Count; directiveIndex++)
        {
            var importNode = importNodesByDirectiveIndex[directiveIndex];

            if (!TryGetImportSpliceBounds(importNode, out var prefixEnd, out var suffixStart, out var fullEnd))
            {
                segmentsForResource = [new SourceSegment(resource.Id, OriginalStartOffset: 0, content.TextLength)];
                return content;
            }

            if (prefixEnd < cursor || fullEnd < suffixStart)
            {
                segmentsForResource = [new SourceSegment(resource.Id, OriginalStartOffset: 0, content.TextLength)];
                return content;
            }

            // Emit everything up through the end of the first token's leading trivia.
            // This trivia will be preserved (transferred onto the first inserted node).
            if (prefixEnd > cursor)
            {
                outputSegments.Add(new SourceSegment(resource.Id, OriginalStartOffset: cursor, prefixEnd - cursor));
            }

            var key = new MergeContext<SyntaxTree, ImportDirective>.ResolvedReferenceKey(resource.Id, directiveIndex);
            if (context.ResolvedReferences.TryGetValue(key, out var resolvedId)
                && processedSegments.TryGetValue(resolvedId, out var resolvedSegments))
            {
                outputSegments.AddRange(resolvedSegments);
            }

            // Emit the last token's trailing trivia, which will also be preserved (transferred onto the last inserted node).
            if (fullEnd > suffixStart)
            {
                outputSegments.Add(new SourceSegment(resource.Id, OriginalStartOffset: suffixStart, fullEnd - suffixStart));
            }

            // Advance cursor past the entire import node span (including trivia and text).
            cursor = fullEnd;
        }

        if (cursor < content.TextLength)
        {
            outputSegments.Add(new SourceSegment(resource.Id, OriginalStartOffset: cursor, content.TextLength - cursor));
        }

        segmentsForResource = outputSegments;

        // Create editor for this tree
        var editor = content.CreateEditor(content.Schema!.ToTokenizerOptions());

        for (var directiveIndex = importNodesByDirectiveIndex.Count - 1; directiveIndex >= 0; directiveIndex--)
        {
            var importNode = importNodesByDirectiveIndex[directiveIndex];
            var reference = _getReference(importNode);

            var key = new MergeContext<SyntaxTree, ImportDirective>.ResolvedReferenceKey(resource.Id, directiveIndex);
            if (!context.ResolvedReferences.TryGetValue(key, out var resolvedId))
            {
                var location = importNode.Position..importNode.Position;
                string? lineColumn = null;
                if (SyntaxTreeLineColumnMapper.TryFormatRange(content, resource.Id, location, LineBoundaryResolver, out var formatted))
                {
                    lineColumn = formatted;
                }

                context.Diagnostics.Add(
                    new MergeDiagnostic(
                        resource.Id,
                        location,
                        $"Missing resolved reference mapping for import: {reference}")
                    {
                        LineColumnLocation = lineColumn,
                    });

                editor.Remove(importNode);
                continue;
            }

            if (processedTrees.TryGetValue(resolvedId, out var resolvedTree))
            {
                var childrenToInline = resolvedTree.Root.Children.ToList();
                if (childrenToInline.Count > 0)
                {
                    editor.Replace(importNode, childrenToInline);
                }
                else
                {
                    editor.Remove(importNode);
                }

                continue;
            }

            // Dependency is not available in processedTrees; fall back to resolved cache.
            // If it is missing there too, this is a merge-time error.
            if (!context.ResolvedCache.TryGetValue(resolvedId, out _))
            {
                var location = importNode.Position..importNode.Position;
                string? lineColumn = null;
                if (SyntaxTreeLineColumnMapper.TryFormatRange(content, resource.Id, location, LineBoundaryResolver, out var formatted))
                {
                    lineColumn = formatted;
                }

                context.Diagnostics.Add(
                    new MergeDiagnostic(
                        resource.Id,
                        location,
                        $"Could not resolve import reference: {reference} (resolved id: {resolvedId.Path})")
                    {
                        LineColumnLocation = lineColumn,
                    });

                editor.Remove(importNode);
            }
            else
            {
                // The resolved resource exists but wasn't processed yet.
                // Keep behavior conservative: remove the import and report an error rather than producing partial output.
                var location = importNode.Position..importNode.Position;
                string? lineColumn = null;
                if (SyntaxTreeLineColumnMapper.TryFormatRange(content, resource.Id, location, LineBoundaryResolver, out var formatted))
                {
                    lineColumn = formatted;
                }

                context.Diagnostics.Add(
                    new MergeDiagnostic(
                        resource.Id,
                        location,
                        $"Resolved dependency was not processed in merge order: {resolvedId.Path}")
                    {
                        LineColumnLocation = lineColumn,
                    });

                editor.Remove(importNode);
            }
        }

        // Commit the edits
        editor.Commit();

        return content;
    }

    private static void BuildSourceMapFromSegments(
        SyntaxTree mergedTree,
        ResourceId rootResourceId,
        IReadOnlyDictionary<ResourceId, IReadOnlyList<SourceSegment>> processedSegments,
        MergeContext<SyntaxTree, ImportDirective> context)
    {
        if (!processedSegments.TryGetValue(rootResourceId, out var rootSegments) || rootSegments.Count == 0)
        {
            context.SourceMapBuilder.AddOffsetSegment(
                rootResourceId,
                generatedStartOffset: 0,
                originalStartOffset: 0,
                length: mergedTree.TextLength);
            return;
        }

        var generatedOffset = 0;
        for (var i = 0; i < rootSegments.Count; i++)
        {
            var seg = rootSegments[i];
            context.SourceMapBuilder.AddOffsetSegment(
                seg.ResourceId,
                generatedStartOffset: generatedOffset,
                originalStartOffset: seg.OriginalStartOffset,
                length: seg.Length);

            generatedOffset += seg.Length;
        }

        if (generatedOffset != mergedTree.TextLength)
        {
            // Fallback: ensure full coverage at least for diagnostics.
            // Note: we cannot remove previously added segments, so add a last-resort segment for the remainder.
            if (generatedOffset < mergedTree.TextLength)
            {
                context.SourceMapBuilder.AddOffsetSegment(
                    rootResourceId,
                    generatedStartOffset: generatedOffset,
                    originalStartOffset: 0,
                    length: mergedTree.TextLength - generatedOffset);
            }
        }
    }
}
