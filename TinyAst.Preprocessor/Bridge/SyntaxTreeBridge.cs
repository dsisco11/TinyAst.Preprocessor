using TinyAst.Preprocessor.Bridge.Imports;
using TinyAst.Preprocessor.Bridge.Merging;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Bridge;

/// <summary>
/// Convenience wrapper that binds a reference-extractor once and provides
/// bridge components for schema-bound <see cref="SyntaxTree"/> preprocessing.
/// </summary>
/// <typeparam name="TImportNode">The downstream import node type.</typeparam>
/// <typeparam name="TContext">The user context type passed through preprocessing.</typeparam>
public sealed class SyntaxTreeBridge<TImportNode, TContext>
    where TImportNode : SyntaxNode
{
    /// <summary>
    /// Gets the directive parser configured for <typeparamref name="TImportNode"/>.
    /// </summary>
    public ImportDirectiveParser<TImportNode> Parser { get; }

    /// <summary>
    /// Gets the merge strategy configured for <typeparamref name="TImportNode"/>.
    /// </summary>
    public SyntaxTreeMergeStrategy<TImportNode, TContext> MergeStrategy { get; }

    /// <summary>
    /// Creates a new bridge wrapper.
    /// </summary>
    /// <param name="getReference">
    /// Delegate that extracts the import reference from a downstream import node.
    /// Returning null/empty/whitespace causes the node to be ignored.
    /// </param>
    public SyntaxTreeBridge(Func<TImportNode, string?> getReference)
    {
        ArgumentNullException.ThrowIfNull(getReference);

        Parser = new ImportDirectiveParser<TImportNode>(getReference);
        MergeStrategy = new SyntaxTreeMergeStrategy<TImportNode, TContext>(getReference);
    }
}
