# TinyAst.Preprocessor

AST-native preprocessor bridge between [TinyAst](https://github.com/user/TinyAst) (TinyTokenizer.Ast) and [TinyPreprocessor](https://github.com/user/TinyPreprocessor).

## Requirements

- **TinyPreprocessor 0.3.0+** (generic content API)
- **TinyAst 0.11.0+** (schema binding, SyntaxEditor)
- **.NET 8.0+**

## Overview

This bridge enables AST-native import/include preprocessing:

- Import directives are detected via schema-bound syntax trees, not text parsing
- Downstream consumers define imports by binding a node type in their schema, and providing a reference-extractor delegate
- Merging uses `SyntaxEditor` to inline resolved content directly into the AST
- Source maps track original locations through the merge process

## Quick Start

### 1. Define Your Import Node

Create a `SyntaxNode` type for your import directive shape (no required interface):

```csharp
using TinyTokenizer.Ast;

public sealed class MyImportNode : SyntaxNode
{
    public MyImportNode(CreationContext context) : base(context) { }

    public string Reference
    {
        get
        {
            // Extract the import path from your node's children
            // Example: import "path/to/file"
            var stringToken = Children
                .OfType<SyntaxToken>()
                .FirstOrDefault(t => t.Kind == NodeKind.String);

            if (stringToken is null)
                return string.Empty;

            var text = stringToken.Text;
            // Strip quotes
            return text.Length >= 2 && text[0] == '"' && text[^1] == '"'
                ? text[1..^1]
                : text;
        }
    }
}
```

### 2. Register in Your Schema

```csharp
var importPattern = new PatternBuilder()
    .Ident("import")
    .String()
    .BuildQuery();

var schema = Schema.Create()
    .DefineSyntax<MyImportNode>("Import", b => b.Match(importPattern))
    .Build();
```

### 3. Wire Up the Preprocessor

```csharp
using TinyAst.Preprocessor.Bridge;
using TinyAst.Preprocessor.Bridge.Resources;
using TinyPreprocessor.Core;

// Create resource store and add your resources
var store = new InMemorySyntaxTreeResourceStore();

var mainTree = SyntaxTree.ParseAndBind(mainSource, schema);
var mainResource = new Resource<SyntaxTree>(new ResourceId("main"), mainTree);
store.Add(mainResource);

// Add other resources that can be imported
var libTree = SyntaxTree.ParseAndBind(libSource, schema);
store.Add(new Resource<SyntaxTree>(new ResourceId("lib"), libTree));

// Create the preprocessor (defaults wired for SyntaxTree content)
var preprocessor = new SyntaxTreePreprocessor<MyImportNode>(
    store,
    getReference: n => n.Reference);

// Process
var result = await preprocessor.ProcessAsync(
    mainResource,
    context: null!,
    PreprocessorOptions.Default);

if (result.Success)
{
    // result.Content is the merged SyntaxTree
    Console.WriteLine(result.Content.ToText());
}
else
{
    foreach (var diagnostic in result.Diagnostics)
    {
        Console.WriteLine(diagnostic);
    }
}
```

## Public API

### Core Types

| Type                                 | Namespace          | Description                                         |
| ------------------------------------ | ------------------ | --------------------------------------------------- |
| `SyntaxTreePreprocessor<T>`          | `Bridge`           | One-stop preprocessor setup (context = object)      |
| `SyntaxTreePreprocessor<T,C>`        | `Bridge`           | One-stop preprocessor setup for SyntaxTree content  |
| `SyntaxTreeBridge<T,C>`              | `Bridge`           | Convenience wrapper (parser + merge strategy)       |
| `ImportDirective`                    | `Bridge.Imports`   | Directive record with Reference, Location, Resource |
| `ImportDirectiveModel`               | `Bridge.Imports`   | `IDirectiveModel<ImportDirective>` implementation   |
| `ImportDirectiveParser<T>`           | `Bridge.Imports`   | `IDirectiveParser<SyntaxTree, ImportDirective>`     |
| `SyntaxTreeContentModel`             | `Bridge.Content`   | `IContentModel<SyntaxTree>` implementation          |
| `SyntaxTreeMergeStrategy<T,C>`       | `Bridge.Merging`   | `IMergeStrategy` using SyntaxEditor                 |
| `InMemorySyntaxTreeResourceStore`    | `Bridge.Resources` | In-memory resource store for testing                |
| `InMemorySyntaxTreeResourceResolver` | `Bridge.Resources` | Resolver using the in-memory store                  |

### Key Constraints

- **Schema binding is required**: Trees must have `HasSchema == true`
- **Locations are absolute offsets**: TinyAst node coordinates (trivia-inclusive)
- **No text reparsing**: Merge operates directly on AST nodes via `SyntaxEditor`

## Documentation

- [Bridge Overview](docs/bridge/01-overview.md)
- [ImportNode + Schema Binding](docs/bridge/02-importnode-and-schema.md)
- [Generic TinyPreprocessor Contracts](docs/bridge/03-generic-preprocessor-contracts.md)
- [Source Mapping + Diagnostics](docs/bridge/04-source-mapping-and-diagnostics.md)
- [Implementation Plan](docs/bridge/05-implementation-plan.md)

## License

See [LICENSE.txt](LICENSE.txt).
