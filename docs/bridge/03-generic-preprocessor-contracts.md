# Generic TinyPreprocessor Contracts (Assumed)

This bridge is designed under the assumption that TinyPreprocessor will be made **generic over content** (and, ideally, over location types) so it can operate on AST content directly.

## Why Generics Are Needed

The current TinyPreprocessor surface is text-oriented:

- `IResource.Content : ReadOnlyMemory<char>`
- `IDirective.Location : Range` (offsets into the text)
- Merge strategies output `ReadOnlyMemory<char>`

To support a SyntaxTree/SyntaxNode-driven pipeline without materializing text, TinyPreprocessor needs to operate on a non-text content model.

## Proposed Minimal Generic Shape

A workable minimal generic set (names illustrative):

- `IResource<TContent>`

  - `ResourceId Id`
  - `TContent Content`
  - `IReadOnlyDictionary<string, object> Metadata`

- `IDirective<TLocation>`

  - `TLocation Location`

- `IIncludeDirective<TLocation> : IDirective<TLocation>`

  - `string Reference`

- `IDirectiveParser<TDirective, TContent>`

  - `IEnumerable<TDirective> Parse(TContent content, ResourceId resourceId)`

- `IMergeStrategy<TContext, TContent, TLocation>`

  - `TContent Merge(IReadOnlyList<ResolvedResource<TContent, TLocation>> ordered, TContext context, MergeContext<TLocation> mergeContext)`

- `PreprocessResult<TContent, TLocation>`
  - `TContent Content`
  - `ISourceMap<TLocation> SourceMap`
  - `DiagnosticCollection<TLocation> Diagnostics`

This is intentionally minimal: it preserves TinyPreprocessor’s architecture while swapping out the content type.

## Location Type for AST Pipelines

For an AST pipeline, the bridge assumes locations are expressed in **TinyAst node coordinates**.

A practical location type:

- `(ResourceId Resource, int Position)`

Optionally expandable later to:

- `(ResourceId Resource, int Position, int Length)`

Key decision (bridge): **anchor to node start** and treat length as 0 by default.

## Notes on SyntaxNode as Content

Using `SyntaxNode` as `TContent` is possible but has tradeoffs:

- Red nodes are _ephemeral wrappers_ that point back to a `SyntaxTree`.
- Merging across trees may be easier using green nodes (`GreenNode`) or a merged `SyntaxTree` root.

If TinyPreprocessor requires a single `TContent` for the merged result, consider making `TContent`:

- `SyntaxTree` (merged tree)
- or `GreenNode` (merged green root)

This doc uses `SyntaxNode` as the conceptual driver because that matches the desired API shape, but the final choice should optimize for immutability and ease of merging.
