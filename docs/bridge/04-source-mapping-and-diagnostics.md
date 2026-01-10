# Source Mapping + Diagnostics (Node Coordinates)

## Coordinate Space

All coordinates are in **TinyAst syntax tree coordinates**:

- Absolute character offsets as reported by `SyntaxNode.Position`.
- Positions include trivia (whitespace/comments), by design.

## Directive Locations

An import/include directive location is defined as:

- `Position = ImportNode.Position`
- `Location = (Position)` (a zero-length location)

This means:

- Diagnostics can point at a single anchor position.
- The system does not attempt to remove directive text/spans at this level.

> Note: If TinyPreprocessor continues to strip directive text/ranges, a separate “strip span” concept may be required.
> For an AST-native merge strategy, stripping can be implemented structurally (removing the import nodes from the merged AST).

## Diagnostic Pinning

All diagnostics produced by the bridge should be pinned to:

- The `ResourceId` of the resource containing the `ImportNode`.
- The `ImportNode.Position` anchor.

Examples:

- Reference extraction failed (malformed import node)
- Resolver failed to resolve a reference

## Source Mapping for AST Merge

If TinyPreprocessor becomes AST-native:

- Source mapping should relate **positions in the merged AST** back to **positions in original AST resources**.

A pragmatic approach is to treat mapping as segments:

- `GeneratedPosition -> (OriginalResourceId, OriginalPosition)`

Where positions are integer offsets in the associated tree.

Implementation options:

1. **Offset-segment mapping** (closest to current TinyPreprocessor)

   - When emitting/merging, track how much text/width each source contributes.

2. **Node-origin mapping** (AST-first)
   - Maintain origin metadata per merged node (resource + start position).

The bridge prefers (2) if merge is structural.

## Invariants

- All positions must be interpreted relative to the content’s coordinate space.
- If any normalization occurs (e.g., newline rewriting), it must happen before position computation and be applied consistently.
