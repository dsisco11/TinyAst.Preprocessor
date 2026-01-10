using TinyAst.Preprocessor.Bridge.Resources;
using TinyAst.Preprocessor.Bridge.Imports;
using TinyPreprocessor;
using TinyPreprocessor.Core;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Bridge;

/// <summary>
/// Convenience wrapper over <see cref="SyntaxTreePreprocessor{TImportNode, TContext}"/> that uses
/// <see cref="object"/> as the context type.
/// </summary>
/// <typeparam name="TImportNode">The downstream import node type (schema-bound).</typeparam>
public sealed class SyntaxTreePreprocessor<TImportNode>
    where TImportNode : SyntaxNode
{
    private readonly SyntaxTreePreprocessor<TImportNode, object> _inner;

    /// <summary>
    /// Creates a preprocessor using the provided resolver.
    /// </summary>
    public SyntaxTreePreprocessor(
        IResourceResolver<SyntaxTree> resolver,
        Func<TImportNode, string?> getReference)
    {
        _inner = new SyntaxTreePreprocessor<TImportNode, object>(resolver, getReference);
    }

    /// <summary>
    /// Creates a preprocessor backed by an in-memory store (handy for tests and samples).
    /// </summary>
    public SyntaxTreePreprocessor(
        InMemorySyntaxTreeResourceStore store,
        Func<TImportNode, string?> getReference,
        ImportDirectiveLocationIndex? locationIndex = null)
    {
        _inner = new SyntaxTreePreprocessor<TImportNode, object>(store, getReference, locationIndex);
    }

    /// <summary>
    /// Runs preprocessing starting from <paramref name="root"/>.
    /// </summary>
    public ValueTask<PreprocessResult<SyntaxTree>> ProcessAsync(
        IResource<SyntaxTree> root,
        object? context = null,
        PreprocessorOptions? options = null,
        CancellationToken ct = default)
    {
        return _inner.ProcessAsync(
            root,
            context ?? new object(),
            options,
            ct);
    }
}
