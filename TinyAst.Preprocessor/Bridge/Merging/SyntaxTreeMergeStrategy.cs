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

        foreach (var resource in orderedResources)
        {
            var processedTree = ProcessResource(resource, processedTrees, context);
            processedTrees[resource.Id] = processedTree;
        }

        var mergedTree = processedTrees[rootResource.Id];

        // Build source map for the merged result
        BuildSourceMap(mergedTree, orderedResources, context);

        return mergedTree;
    }

    private SyntaxTree ProcessResource(
        ResolvedResource<SyntaxTree, ImportDirective> resource,
        Dictionary<ResourceId, SyntaxTree> processedTrees,
        MergeContext<SyntaxTree, ImportDirective> context)
    {
        OnProcessResource(resource.Id);

        var content = resource.Content;

        if (!content.HasSchema)
        {
            // Cannot process unbound trees
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
            return content;
        }

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

    private static void BuildSourceMap(
        SyntaxTree mergedTree,
        IReadOnlyList<ResolvedResource<SyntaxTree, ImportDirective>> orderedResources,
        MergeContext<SyntaxTree, ImportDirective> context)
    {
        // For now, record the full merged content as coming from the root resource
        // A more sophisticated implementation could track individual node origins
        var rootResource = orderedResources[^1];
        context.SourceMapBuilder.AddOffsetSegment(
            rootResource.Id,
            generatedStartOffset: 0,
            originalStartOffset: 0,
            length: mergedTree.TextLength);
    }
}
