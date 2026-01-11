# Generic TinyPreprocessor Contracts (TinyPreprocessor 0.4.0)

TinyPreprocessor 0.4.0 is generic over **content** and includes merge-time resolved-id mapping. This bridge targets that API surface.

## Why Content Generics Matter

Historically, TinyPreprocessor operated on text content. With 0.3.0 it became generic over content via `IContentModel<TContent>`, and in 0.4.0 it adds merge-time resolved-id mapping plus optional boundary resolution.

## Actual Core Abstractions

TinyPreprocessor 0.4.0 is built around these abstractions:

- `IResource<TContent>`

  - `ResourceId Id`
  - `TContent Content`
  - `IReadOnlyDictionary<string, object> Metadata`

- `IDirectiveParser<TContent, TDirective>`

  - `IEnumerable<TDirective> Parse(TContent content, ResourceId resourceId)`

- `IDirectiveModel<TDirective>`

  - `Range GetLocation(TDirective directive)`
  - `bool TryGetReference(TDirective directive, out string reference)`

- `IResourceResolver<TContent>`

  - `ValueTask<ResourceResolutionResult<TContent>> ResolveAsync(string reference, IResource<TContent>? context, CancellationToken ct)`

- `IMergeStrategy<TContent, TDirective, TContext>`

  - `TContent Merge(IReadOnlyList<ResolvedResource<TContent, TDirective>> orderedResources, TContext userContext, MergeContext<TContent, TDirective> mergeContext)`

- `IContentModel<TContent>`

  - `int GetLength(TContent content)`
  - `TContent Slice(TContent content, int start, int length)`

- `IContentBoundaryResolver<TContent, TBoundary>`

  - `IEnumerable<int> ResolveOffsets(TContent content, ResourceId resourceId, int startOffset, int endOffset)`

- `IContentBoundaryResolverProvider`

  - `bool TryGet<TContent, TBoundary>(out IContentBoundaryResolver<TContent, TBoundary> resolver)`

- `Preprocessor<TContent, TDirective, TContext>`

  - `Task<PreprocessResult<TContent>> ProcessAsync(IResource<TContent> root, TContext context, PreprocessorOptions? options = null, CancellationToken ct = default)`

- `PreprocessResult<TContent>`
  - `TContent Content`
  - `SourceMap SourceMap`
  - `DiagnosticCollection Diagnostics`

The important implication for this bridge is that **locations are still expressed as `System.Range`**. The meaning of those offsets is defined by `IContentModel<TContent>`.

TinyPreprocessor v0.4 also introduces a second, optional axis for interpreting locations:

- Logical boundary kinds (e.g., `TinyPreprocessor.Text.LineBoundary`) resolved via `IContentBoundaryResolverProvider`.

## Locations for AST Pipelines

For an AST pipeline, this bridge interprets TinyPreprocessor locations as:

- absolute TinyAst character offsets (including trivia), i.e. `SyntaxNode.Position`

Directive locations are anchored to node start:

- `Range = pos..pos`

This works naturally with `IDirectiveModel<TDirective>.GetLocation(...)`.

## Notes on AST as Content

For this bridge, `TContent` should be chosen to work well with `SyntaxEditor`.

Practical options:

- `SyntaxTree` (recommended) — merge strategy edits a `SyntaxTree` via `SyntaxEditor`.
- `SyntaxNode` — possible, but red nodes are wrappers tied to a tree.

Whichever is chosen, `IContentModel<TContent>` must define how to:

- compute length (`GetLength`)
- slice (`Slice`) in the same offset space used by `SyntaxNode.Position`

## Merge Identity in v0.4

TinyPreprocessor v0.4 treats dependency identity as resolver-owned:

- `ResourceId` is an opaque identity (not required to be path-like).
- Resolvers may canonicalize references.
- Merge uses `MergeContext.ResolvedReferences` (keyed by directive occurrence) to select the dependency `ResourceId` to inline.
