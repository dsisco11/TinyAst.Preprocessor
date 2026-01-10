using TinyAst.Preprocessor.Bridge.Imports;
using TinyAst.Preprocessor.Bridge.Resources;
using TinyPreprocessor.Core;
using TinyPreprocessor.Merging;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Bridge.Merging;

/// <summary>
/// Implements <see cref="IMergeStrategy{TContent, TDirective, TContext}"/> for <see cref="SyntaxTree"/>
/// by replacing import nodes with the content from resolved resources using <see cref="SyntaxEditor"/>.
/// </summary>
/// <typeparam name="TImportNode">
/// The downstream import node type implementing <see cref="IImportNode"/>.
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
public sealed class SyntaxTreeMergeStrategy<TImportNode, TContext> : IMergeStrategy<SyntaxTree, ImportDirective, TContext>
    where TImportNode : SyntaxNode, IImportNode
{
    /// <summary>
    /// Gets the singleton instance of the merge strategy.
    /// </summary>
    public static SyntaxTreeMergeStrategy<TImportNode, TContext> Instance { get; } = new();

    private SyntaxTreeMergeStrategy() { }

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

        // Build a lookup of processed trees by resource ID
        // Process in dependency order so nested imports are already expanded
        var processedTrees = new Dictionary<ResourceId, SyntaxTree>();

        foreach (var resource in orderedResources)
        {
            if (resource == rootResource)
            {
                continue;
            }

            var processedTree = ProcessResource(resource, processedTrees, context);
            processedTrees[resource.Id] = processedTree;
        }

        // Process the root resource last
        var mergedTree = ProcessResource(rootResource, processedTrees, context);

        // Build source map for the merged result
        BuildSourceMap(mergedTree, orderedResources, context);

        return mergedTree;
    }

    private static SyntaxTree ProcessResource(
        ResolvedResource<SyntaxTree, ImportDirective> resource,
        Dictionary<ResourceId, SyntaxTree> processedTrees,
        MergeContext<SyntaxTree, ImportDirective> context)
    {
        var content = resource.Content;

        if (!content.HasSchema)
        {
            // Cannot process unbound trees
            return content;
        }

        // Find import nodes in this tree
        var importNodes = content
            .Select(Query.Syntax<TImportNode>())
            .OfType<TImportNode>()
            .OrderByDescending(n => n.Position) // Process in reverse order to preserve positions
            .ThenByDescending(n => n.SiblingIndex)
            .ToList();

        if (importNodes.Count == 0)
        {
            return content;
        }

        // Create editor for this tree
        var editor = content.CreateEditor(content.Schema!.ToTokenizerOptions());

        foreach (var importNode in importNodes)
        {
            var reference = importNode.Reference;
            if (string.IsNullOrWhiteSpace(reference))
            {
                continue;
            }

            // Resolve the reference to find the replacement tree
            var resolvedId = ResourceIdPathResolver.Resolve(reference, resource.Resource);

            if (processedTrees.TryGetValue(resolvedId, out var resolvedTree))
            {
                // Get the children of the resolved tree's root to inline
                var childrenToInline = resolvedTree.Root.Children.ToList();

                if (childrenToInline.Count > 0)
                {
                    // Replace the import node with the resolved content's children
                    editor.Replace(importNode, childrenToInline);
                }
                else
                {
                    // Empty resolved content - just remove the import
                    editor.Remove(importNode);
                }
            }
            else
            {
                // Reference not found - report diagnostic and remove the import
                context.Diagnostics.Add(
                    new MergeDiagnostic(
                        resource.Id,
                        importNode.Position..importNode.Position,
                        $"Could not resolve import reference: {reference}"));

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
