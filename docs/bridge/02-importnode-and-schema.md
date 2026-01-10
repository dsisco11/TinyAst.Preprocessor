# Import Node Types + Schema Binding

## Requirement: Schema Binding

This bridge **requires schema binding**.

Rationale:

- Typed syntax node discovery (`ImportNode`) relies on TinyAst’s schema binder creating syntax nodes of the correct kind.
- Unbound trees do not have a reliable, stable way to represent language-level constructs as typed nodes.

Downstream consumers should parse and bind using their schema (exact API depends on TinyAst version).

## Import Node Contract

Downstream consumers define their own import node type (e.g., `MyImportNode`) and provide the bridge with a way to extract the import reference.

The bridge treats a downstream import node as:

- **A directive anchor** (location/diagnostics attach here)
- **A directive container** (children/tokens contain the reference)

The bridge does **not** assume:

- A specific token sequence
- Quoting rules
- Whether the import is line-based or statement-based

## Discovering Import Nodes

Directive extraction uses TinyAst Query and a bridge-provided parser:

- `ImportDirectiveParser<TImportNode>`

Internally, the resolver uses TinyAst's built-in Query system:

- `tree.Select(Query.Syntax<TImportNode>())`

The bridge should treat discovery as:

- Pure read-only traversal
- Deterministic ordering (document order)

## Extracting the Reference

Downstream consumers expose the reference by providing a delegate:

- `Func<TImportNode, string?> getReference`

If the reference is empty/whitespace:

- The bridge will ignore the node (until TinyPreprocessor provides a way for parsers/extractors to emit diagnostics).

## Location Policy

All locations (directive locations and diagnostics) are in node coordinates.

Default policy:

- `Location = Position..Position` where `Position` is the node's `SyntaxNode.Position`.

This anchors diagnostics precisely and avoids having to choose a “whole-node” span.

## Example (Conceptual)

Downstream schema authors define a pattern for their language and bind it to their node type (e.g., `MyImportNode`).

Examples might include:

- `#import "path"`
- `import "path";`
- `@use(path)`

The bridge does not hardcode these; it only requires that matching syntax binds to the downstream node type and that a reference extractor is provided.
