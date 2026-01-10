# Generic TinyPreprocessor Contracts (TinyPreprocessor 0.3.0)

TinyPreprocessor 0.3.0 is generic over **content**. This bridge will target that API surface.

## Why Content Generics Matter

Historically, TinyPreprocessor operated on text content. With 0.3.0, the pipeline can operate on any `TContent` as long as an `IContentModel<TContent>` is provided.

## Actual Core Abstractions

TinyPreprocessor 0.3.0 is built around these abstractions:

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

- `Preprocessor<TContent, TDirective, TContext>`

  - `Task<PreprocessResult<TContent>> ProcessAsync(IResource<TContent> root, TContext context, PreprocessorOptions? options = null, CancellationToken ct = default)`

- `PreprocessResult<TContent>`
  - `TContent Content`
  - `SourceMap SourceMap`
  - `DiagnosticCollection Diagnostics`

The important implication for this bridge is that **locations are still expressed as `System.Range`**. The meaning of those offsets is defined by `IContentModel<TContent>`.

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
