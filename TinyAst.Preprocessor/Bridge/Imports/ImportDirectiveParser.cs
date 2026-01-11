using TinyPreprocessor.Core;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Bridge.Imports;

/// <summary>
/// Parses <see cref="ImportDirective"/> instances from a schema-bound <see cref="SyntaxTree"/>.
/// </summary>
/// <typeparam name="TImportNode">The downstream import node type.</typeparam>
public sealed class ImportDirectiveParser<TImportNode> : IDirectiveParser<SyntaxTree, ImportDirective>
    where TImportNode : SyntaxNode
{
    private readonly Func<TImportNode, string?> _getReference;

    /// <summary>
    /// Creates a directive parser using a delegate that extracts the import reference from <typeparamref name="TImportNode"/>.
    /// </summary>
    /// <param name="getReference">
    /// Delegate that returns a reference string for an import node. Returning null/empty/whitespace causes the node to be ignored.
    /// </param>
    public ImportDirectiveParser(Func<TImportNode, string?> getReference)
    {
        _getReference = getReference ?? throw new ArgumentNullException(nameof(getReference));
    }

    /// <summary>
    /// Parses import directives from <paramref name="content"/> in document order.
    /// </summary>
    /// <param name="content">The schema-bound syntax tree.</param>
    /// <param name="resourceId">The owning resource id.</param>
    /// <returns>A sequence of directives in deterministic document order.</returns>
    public IEnumerable<ImportDirective> Parse(SyntaxTree content, ResourceId resourceId)
    {
        if (!content.HasSchema)
        {
            throw new InvalidOperationException(
                "Directive parsing requires a schema-bound SyntaxTree (HasSchema == true).");
        }

        var importNodes = content
            .Select(Query.Syntax<TImportNode>())
            .OfType<TImportNode>()
            .OrderBy(n => n.Position)
            .ThenBy(n => n.SiblingIndex);

        foreach (var node in importNodes)
        {
            var reference = _getReference(node);
            if (string.IsNullOrWhiteSpace(reference))
            {
                continue;
            }

            var position = node.Position;
            yield return new ImportDirective(reference, position..position, resourceId);
        }
    }
}
