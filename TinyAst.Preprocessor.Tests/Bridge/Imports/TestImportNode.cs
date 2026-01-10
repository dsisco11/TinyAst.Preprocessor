using TinyAst.Preprocessor.Bridge.Imports;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Tests.Bridge.Imports;

public sealed class TestImportNode : SyntaxNode, IImportNode
{
    public TestImportNode(CreationContext context)
        : base(context)
    {
    }

    public string Reference
    {
        get
        {
            var stringToken = Children
                .OfType<SyntaxToken>()
                .FirstOrDefault(t => t.Kind == NodeKind.String);

            if (stringToken is null)
            {
                return string.Empty;
            }

            var text = stringToken.Text;
            if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
            {
                return text[1..^1];
            }

            return text;
        }
    }
}
