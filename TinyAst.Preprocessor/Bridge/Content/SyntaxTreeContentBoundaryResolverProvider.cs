using TinyPreprocessor.Core;
using TinyPreprocessor.Text;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Bridge.Content;

public sealed class SyntaxTreeContentBoundaryResolverProvider : IContentBoundaryResolverProvider
{
    public static SyntaxTreeContentBoundaryResolverProvider Instance { get; } = new();

    private static readonly SyntaxTreeLineBoundaryResolver LineBoundaryResolver = new();

    private SyntaxTreeContentBoundaryResolverProvider()
    {
    }

    public bool TryGet<TContent, TBoundary>(out IContentBoundaryResolver<TContent, TBoundary> resolver)
    {
        if (typeof(TContent) == typeof(SyntaxTree) && typeof(TBoundary) == typeof(LineBoundary))
        {
            resolver = (IContentBoundaryResolver<TContent, TBoundary>)(object)LineBoundaryResolver;
            return true;
        }

        resolver = default!;
        return false;
    }
}
