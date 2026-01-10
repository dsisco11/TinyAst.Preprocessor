# TinyAst.Preprocessor Bridge — Overview

## Purpose

This repository provides an integration bridge between:

- **TinyAst** (`TinyTokenizer.Ast`) — a schema-bound syntax tree with typed `SyntaxNode` wrappers and queries.
- **TinyPreprocessor** — a preprocessing pipeline that resolves dependencies, merges content, and produces diagnostics + source mapping.

The bridge direction is **SyntaxTree → Preprocessor**.

The key constraint is that **the syntax tree is the source of truth for import/include directives**. Import directives are detected and represented by the syntax tree (via schema-bound node types), not by re-parsing text.

## Non-Goals

- Define a language-specific import grammar. Downstream consumers own the import shape.
- Provide file-system/network resolution defaults beyond an explicit resolver contract.

## Core Idea: IImportNode + ImportNodeResolver (Schema Opt-In)

TinyAst does not ship a built-in include/import node. Downstream consumers define their own.

This bridge defines a canonical interface for downstream import nodes:

- `IImportNode` (implemented by a downstream `SyntaxNode` type)

Downstream consumers opt-in by:

1. Defining syntax patterns in their TinyAst `Schema` that bind to their own `SyntaxNode` type (e.g., `MyImportNode`).
2. Implementing `IImportNode` on that node type to expose the resolved reference string.

The bridge then discovers imports by using `ImportNodeResolver<TImportNode>` to locate nodes of that type in the schema-bound tree and convert them into directives.

## Coordinate Space + Locations

All directive locations and diagnostic locations are expressed in **TinyAst node coordinates**, i.e. absolute character offsets from the owning tree.

- Locations anchor to the import node’s **absolute start position** (including trivia).
- Locations are represented as **zero-length ranges** at that start offset.

This enables consistent diagnostic pinning without forcing a “whole-node span” policy.

## Pipeline (Conceptual)

1. **Root resource** is represented as a syntax tree (or root syntax node).
2. **Directive extraction** queries the tree for `ImportNode` instances.
3. **Resolution** maps each import reference to another resource (syntax tree).
4. **Merge** combines resolved resources into a merged output (also in AST form in the generic design).
5. **Result** includes merged content + source map + diagnostics.

## Notes About Generic TinyPreprocessor

These docs assume TinyPreprocessor will evolve to be generic over content (and locations), so it can operate on AST rather than `ReadOnlyMemory<char>`.

- The bridge will target the generic API so it can avoid materializing and re-parsing text.
