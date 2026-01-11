using TinyAst.Preprocessor.Bridge.Content;
using TinyAst.Preprocessor.Bridge.Imports;
using TinyAst.Preprocessor.Bridge.Merging;
using TinyAst.Preprocessor.Bridge.Resources;
using TinyPreprocessor;
using TinyPreprocessor.Core;
using TinyTokenizer.Ast;

namespace TinyAst.Preprocessor.Bridge;

/// <summary>
/// High-level convenience wrapper that wires up a TinyPreprocessor pipeline for schema-bound
/// <see cref="SyntaxTree"/> content.
/// </summary>
/// <typeparam name="TImportNode">The downstream import node type (schema-bound).</typeparam>
/// <typeparam name="TContext">User-provided context type passed through preprocessing.</typeparam>
public sealed class SyntaxTreePreprocessor<TImportNode, TContext>
    where TImportNode : SyntaxNode
{
    private readonly Preprocessor<SyntaxTree, ImportDirective, TContext> _preprocessor;

    /// <summary>
    /// Creates a preprocessor using the provided resolver.
    /// </summary>
    public SyntaxTreePreprocessor(
        IResourceResolver<SyntaxTree> resolver,
        Func<TImportNode, string?> getReference)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(getReference);

        var parser = new ImportDirectiveParser<TImportNode>(getReference);
        var mergeStrategy = new SyntaxTreeMergeStrategy<TImportNode, TContext>(getReference);

        var config = new PreprocessorConfiguration<SyntaxTree, ImportDirective, TContext>(
            parser,
            ImportDirectiveModel.Instance,
            resolver,
            mergeStrategy,
            SyntaxTreeContentModel.Instance);

        _preprocessor = new Preprocessor<SyntaxTree, ImportDirective, TContext>(config);
    }

    /// <summary>
    /// Creates a preprocessor backed by an in-memory store (handy for tests and samples).
    /// </summary>
    public SyntaxTreePreprocessor(
        InMemorySyntaxTreeResourceStore store,
        Func<TImportNode, string?> getReference,
        ImportDirectiveLocationIndex? locationIndex = null)
        : this(new InMemorySyntaxTreeResourceResolver(store, locationIndex), getReference)
    {
    }

    /// <summary>
    /// Runs preprocessing starting from <paramref name="root"/>.
    /// </summary>
    public ValueTask<PreprocessResult<SyntaxTree>> ProcessAsync(
        IResource<SyntaxTree> root,
        TContext context,
        PreprocessorOptions? options = null,
        CancellationToken ct = default)
    {
        return _preprocessor.ProcessAsync(
            root,
            context,
            options ?? PreprocessorOptions.Default,
            ct);
    }
}
