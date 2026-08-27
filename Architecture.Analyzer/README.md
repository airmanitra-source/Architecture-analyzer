# Herimanitra.Architecture.Analyzer

Roslyn analyzers for enforcing architecture and naming conventions in .NET solutions.

## Installation

```shell
dotnet add package Herimanitra.Architecture.Analyzer
```

## Analyzers

| ID | Description |
|----|-------------|
| `ARCH001` | Model type placed in the wrong directory |
| `ARCH002` | File name must match the type name |
| `ARCH003` | Model type in a disallowed folder |
| `ARCH004` | Provider implementation forbidden inside the module |
| `ARCH005` | DTO properties are not properly ordered |
| `ARCH006` | Lines-of-code budget exceeded (class) |
| `ARCH007` | Lines-of-code budget exceeded (method) |
| `ARCH008` | Lines-of-code budget exceeded (solution) |
| `ARCH009` | Lines-of-code budget exceeded (project) |

## Configuration

The analyzer ships an `.editorconfig` with default severity levels. Override them in your project's `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.ARCH001.severity = warning
```

## License

See [LICENSE.txt](LICENSE.txt).
