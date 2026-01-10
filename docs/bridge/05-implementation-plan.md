# Implementation Plan (Bridge)

This document describes how the bridge will be implemented once TinyPreprocessor provides a generic content/location API.

## Status

This project is currently **design-only**.

We will **not implement** the AST-native bridge until TinyPreprocessor ships the generic `IResource<TContent>` / `PreprocessResult<TContent, TLocation>` / `IMergeStrategy<...>` surface.

## Public Types (Bridge)

- `IImportNode`

  - Implemented by downstream `SyntaxNode` types that represent imports/includes.

- `ImportDirective`

  - Default directive produced by the bridge from `IImportNode` (location anchored to node start).

- `ImportNodeResolver<TImportNode>`

  - Discovers downstream import nodes of type `TImportNode` (schema-bound).
  - Converts them into `ImportDirective` using `IImportNode.Reference`.

- `SyntaxTreeResourceStore` + resolver
  - In-memory store keyed by `ResourceId`.
  - Resolver resolves `Reference` to a stored resource (later could be hybrid).

## Processing Flow

1. Root resource is a schema-bound syntax tree.
2. Extract directives from `ImportNode`s.
3. Resolve referenced resources (also syntax trees).
4. Merge resources into a merged AST, removing import nodes structurally.
5. Return merged AST + diagnostics + source map.

## Merge Strategy Notes

For an AST merge strategy, the “concatenate + strip directives” behavior becomes:

- Combine resources in dependency order
- Remove `ImportNode`s from each resource
- Splice remaining nodes into a merged root

All tree mutations are performed using TinyAst’s `SyntaxEditor` system.

Pragmatically, the merge strategy should:

- Start from a single output `SyntaxTree` instance.
- Use `SyntaxEditor` to remove import nodes.
- Use `SyntaxEditor` to insert the resolved content at each import site.

Because `SyntaxNode` wrappers are tied to their original tree, cross-resource splicing must be done in a way that produces output-tree nodes (e.g., by inserting via editor-supported operations). The bridge should avoid manual green-node manipulation and rely on `SyntaxEditor` as the sole mutation mechanism.
