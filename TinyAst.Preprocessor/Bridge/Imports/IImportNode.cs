namespace TinyAst.Preprocessor.Bridge.Imports;

/// <summary>
/// Contract for downstream syntax nodes that represent import/include directives.
/// </summary>
/// <remarks>
/// <para>
/// Downstream consumers define their own import node types (e.g., <c>MyImportNode</c>) 
/// that inherit from <see cref="TinyTokenizer.Ast.SyntaxNode"/> and implement this interface.
/// </para>
/// <para>
/// The bridge uses this interface to extract the reference string from import nodes
/// discovered via TinyAst Query (<c>Query.Syntax&lt;TImportNode&gt;()</c>).
/// </para>
/// </remarks>
public interface IImportNode
{
    /// <summary>
    /// Gets the import reference (e.g., file path, module name).
    /// </summary>
    /// <remarks>
    /// If the reference is empty or whitespace, the bridge will treat this node 
    /// as a non-dependency (ignored during resolution).
    /// </remarks>
    string Reference { get; }
}
