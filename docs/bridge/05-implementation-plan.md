# Implementation Plan (Bridge)

This document describes how the bridge will be implemented using TinyPreprocessor's generic content API (TinyPreprocessor 0.3.0).

## Status

TinyPreprocessor now supports generic content (`TContent`). Implementation can proceed.

## Public Types (Bridge)

- `ImportDirective`

  - Default directive produced by the bridge (location anchored to node start).

- `ImportDirectiveParser<TImportNode>`

  - Discovers downstream import nodes of type `TImportNode` (schema-bound).
  - Converts them into `ImportDirective` using a provided reference extractor delegate.

- `SyntaxTreeResourceStore` + resolver
  - In-memory store keyed by `ResourceId`.
  - Resolver resolves `Reference` to a stored resource (later could be hybrid).

In addition to bridge-specific types, the bridge must provide concrete implementations for TinyPreprocessor's required components:

- `IDirectiveParser<TContent, TDirective>`
- `IDirectiveModel<TDirective>`
- `IResourceResolver<TContent>`
- `IMergeStrategy<TContent, TDirective, TContext>`
- `IContentModel<TContent>`

## Processing Flow

1. Root resource is a schema-bound syntax tree.
2. Extract import directives by querying the tree for downstream import node types (`TImportNode : SyntaxNode`).
3. Resolve referenced resources (also schema-bound trees).
4. Merge resources into a single output tree, removing import nodes and splicing referenced content.
5. Return merged output (`PreprocessResult<TContent>`) with diagnostics + source map.

Key rule: all locations/offsets are in TinyAst node coordinates (absolute character offsets including trivia).

Directive location is anchored to node start:

- `Range = pos..pos` where `pos = importNode.Position`

## Merge Strategy Notes

For an AST merge strategy, the “concatenate + strip directives” behavior becomes:

- Combine resources in dependency order
- Remove import nodes from each resource
- Splice remaining nodes into a merged root

All tree mutations are performed using TinyAst’s `SyntaxEditor` system.

Pragmatically, the merge strategy should:

- Start from a single output `SyntaxTree` instance.
- Use `SyntaxEditor` to remove import nodes.
- Use `SyntaxEditor` to insert the resolved content at each import site.

Because `SyntaxNode` wrappers are tied to their original tree, cross-resource splicing must be done in a way that produces output-tree nodes (e.g., by inserting via editor-supported operations). The bridge should avoid manual green-node manipulation and rely on `SyntaxEditor` as the sole mutation mechanism.

## Implementation Checklist (Concrete)

### 1) Choose `TContent`

Recommended:

- `TContent = SyntaxTree`

Rationale:

- Merge produces a single mutated/constructed tree.
- `SyntaxEditor` operates naturally on a tree.

### 2) Define the directive type

- `ImportDirective` should minimally carry:
  - `ResourceId` (optional but useful for diagnostics)
  - `string Reference`
  - `Range Location` (node-start anchored)

### 3) Implement `IDirectiveParser<SyntaxTree, ImportDirective>`

- Require `tree.HasSchema == true` (schema binding is mandatory).
- Use TinyAst Query to locate import nodes:
  - `tree.Select(Query.Syntax<TImportNode>())`
- Convert each `TImportNode` to `ImportDirective`.

### 4) Implement `IDirectiveModel<ImportDirective>`

- `GetLocation(d)` returns `d.Location`.
- `TryGetReference(d, out reference)` returns true when `Reference` is valid.

This is where we centralize the policy:

- locations are `Position..Position` (trivia-inclusive)
- empty/whitespace references are treated as non-dependencies (or become diagnostics)

### 5) Implement `IResourceResolver<SyntaxTree>`

- Resolve `reference` to a `ResourceId` (relative to `context.Id` when appropriate).
- Return `ResourceResolutionResult<SyntaxTree>.Success(...)` or `.Failure(...)`.

Diagnostic pinning requirement:

- When resolution fails, return `ResolutionFailedDiagnostic(reference, reason, Resource: context?.Id, Location: directive.Location)`.

This implies the resolver must have access to the directive location. If the current resolver signature only provides `(reference, context)`, the bridge will need either:

- a resolver wrapper that closes over a location map (reference -> (resource, range)), or
- a TinyPreprocessor enhancement to thread directive context into resolver calls.

### 6) Implement `IContentModel<SyntaxTree>`

TinyPreprocessor uses `IContentModel<TContent>` to interpret offsets and slices.

For `SyntaxTree`, the bridge should define:

- `GetLength(tree)` in the same coordinate space as `SyntaxNode.Position`.
- `Slice(tree, start, length)` for any internal uses (e.g., directive validation/whole-line checks).

If `Slice` cannot be meaningfully represented as a `SyntaxTree`, define `TContent` as a lightweight content wrapper instead (e.g., `SyntaxTreeDocument`) that can represent a slice view without materializing or re-parsing text.

### 7) Implement `IMergeStrategy<SyntaxTree, ImportDirective, TContext>`

- Apply topological order (already provided to the merge strategy as `orderedResources`).
- Use `SyntaxEditor` to:
  - remove import nodes
  - splice referenced resource content at import sites
- Record source map segments via `mergeContext.SourceMapBuilder`.
- Report diagnostics to `mergeContext.Diagnostics`.

### 8) Add a small end-to-end sample

- A single integration example that:
  - creates a schema with a downstream import node type
  - parses/binds a tree
  - runs the preprocessor and prints merged output

This is the acceptance test for the implementation.
