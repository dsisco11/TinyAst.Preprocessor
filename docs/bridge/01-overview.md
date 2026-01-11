# TinyAst.Preprocessor Bridge — Overview

## Purpose

This repository provides an integration bridge between:

- **TinyAst** (`TinyTokenizer.Ast`) — a schema-bound syntax tree with typed `SyntaxNode` wrappers and queries.
- **TinyPreprocessor** (0.4.0+) — a generic preprocessing pipeline that resolves dependencies, merges content, and produces diagnostics + source mapping.

The bridge direction is **SyntaxTree → Preprocessor**.

The key constraint is that **the syntax tree is the source of truth for import/include directives**. Import directives are detected and represented by the syntax tree (via schema-bound node types), not by re-parsing text.

## Non-Goals

- Define a language-specific import grammar. Downstream consumers own the import shape.
- Provide file-system/network resolution defaults beyond an explicit resolver contract.

## Core Idea: Schema-Bound Nodes + Reference Extractor (Schema Opt-In)

TinyAst does not ship a built-in include/import node. Downstream consumers define their own.

Downstream consumers opt-in by:

1. Defining syntax patterns in their TinyAst `Schema` that bind to their own `SyntaxNode` type (e.g., `MyImportNode`).
2. Providing a reference-extractor delegate `Func<TImportNode, string?>` to the bridge.

The bridge discovers import nodes using `ImportDirectiveParser<TImportNode>` and converts them into `ImportDirective` records by calling the provided extractor.

## Coordinate Space + Locations

All directive locations and diagnostic locations are expressed in **TinyAst node coordinates**, i.e. absolute character offsets from the owning tree.

- Locations anchor to the import node's **absolute start position** (including trivia).
- Locations are represented as **zero-length ranges** at that start offset: `Position..Position`.

This enables consistent diagnostic pinning without forcing a "whole-node span" policy.

## Pipeline (Implemented)

1. **Root resource** is a schema-bound `SyntaxTree` wrapped in `IResource<SyntaxTree>`.
2. **Directive extraction** uses `ImportDirectiveParser<TImportNode>` to query the tree for import nodes via `Query.Syntax<TImportNode>()`.
3. **Resolution** uses `IResourceResolver<SyntaxTree>` to map each import reference to another resource.
4. **Merge** uses `SyntaxTreeMergeStrategy<TImportNode, TContext>` to combine resolved resources via `SyntaxEditor`, directly inlining AST nodes.
5. **Result** includes merged `SyntaxTree` + source map + diagnostics.

## Implemented Components

| Component                            | Description                                                    |
| ------------------------------------ | -------------------------------------------------------------- |
| `ImportDirective`                    | Directive record with Reference, Location, optional Resource   |
| `ImportDirectiveModel`               | `IDirectiveModel<ImportDirective>` implementation              |
| `ImportDirectiveParser<T>`           | `IDirectiveParser<SyntaxTree, ImportDirective>` implementation |
| `SyntaxTreeContentModel`             | `IContentModel<SyntaxTree>` implementation                     |
| `SyntaxTreeMergeStrategy<T,C>`       | `IMergeStrategy` using SyntaxEditor for AST merge              |
| `InMemorySyntaxTreeResourceStore`    | In-memory resource store                                       |
| `InMemorySyntaxTreeResourceResolver` | Resolver backed by in-memory store                             |

## TinyPreprocessor v0.4 merge semantics

TinyPreprocessor v0.4 makes dependency identity explicit at merge-time:

- `ResourceId` is treated as an **opaque identity** during merge (not required to be path-like).
- Resolvers are the **source of truth** for canonical `ResourceId` values.
- Merge uses the resolver-produced resolved-id mapping (`MergeContext.ResolvedReferences`) for each directive occurrence.
- Merge must **not** re-derive dependency IDs from raw reference strings (path heuristics).
