using TinyPreprocessor.Core;
using TinyPreprocessor.Text;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Bridge.Content;

/// <summary>
/// Provides content boundary resolvers for <see cref="SyntaxTree"/> content.
/// </summary>
/// <remarks>
/// This bridge currently supports <see cref="LineBoundary"/> via <see cref="SyntaxTreeLineBoundaryResolver"/>.
/// </remarks>
public sealed class SyntaxTreeContentBoundaryResolverProvider : IContentBoundaryResolverProvider
{
    /// <summary>
    /// Gets the singleton instance of the provider.
    /// </summary>
    public static SyntaxTreeContentBoundaryResolverProvider Instance { get; } = new();

    private static readonly SyntaxTreeLineBoundaryResolver LineBoundaryResolver = new();

    private SyntaxTreeContentBoundaryResolverProvider()
    {
    }

    /// <summary>
    /// Tries to get a boundary resolver for the requested content and boundary marker types.
    /// </summary>
    /// <typeparam name="TContent">The content type.</typeparam>
    /// <typeparam name="TBoundary">The boundary marker type.</typeparam>
    /// <param name="resolver">The resolved boundary resolver, if available.</param>
    /// <returns><see langword="true"/> if a resolver is available; otherwise <see langword="false"/>.</returns>
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
