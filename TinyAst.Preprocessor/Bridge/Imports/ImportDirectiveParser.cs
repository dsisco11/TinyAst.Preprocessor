using TinyPreprocessor.Core;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Bridge.Imports;

public sealed class ImportDirectiveParser<TImportNode> : IDirectiveParser<SyntaxTree, ImportDirective>
    where TImportNode : SyntaxNode
{
    private readonly Func<TImportNode, string?> _getReference;

    public ImportDirectiveParser(Func<TImportNode, string?> getReference)
    {
        _getReference = getReference ?? throw new ArgumentNullException(nameof(getReference));
    }

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
