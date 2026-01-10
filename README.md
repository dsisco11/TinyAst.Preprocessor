# TinyAst.Preprocessor

Integration bridge between `TinyTokenizer.Ast` (TinyAst) and `TinyPreprocessor`.

This bridge is designed around the direction **SyntaxTree → Preprocessor**:

- Import/include directives are detected by the syntax tree (schema-bound), not by re-parsing text.
- Downstream consumers define what constitutes an import by binding their schema patterns to their own `SyntaxNode` type that implements `IImportNode`.

## Status

Design docs only. Implementation is intentionally paused until TinyPreprocessor is generic over content/location.

## Docs

- [Bridge Overview](docs/bridge/01-overview.md)
- [ImportNode + Schema Binding](docs/bridge/02-importnode-and-schema.md)
- [Generic TinyPreprocessor Contracts (Assumed)](docs/bridge/03-generic-preprocessor-contracts.md)
- [Source Mapping + Diagnostics (Node Coordinates)](docs/bridge/04-source-mapping-and-diagnostics.md)
- [Bridge Implementation Plan](docs/bridge/05-implementation-plan.md)
