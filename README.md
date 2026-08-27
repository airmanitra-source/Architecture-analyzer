# Architecture.Analyzer

Roslyn analyzers packaged as [Herimanitra.Architecture.Analyzer](https://www.nuget.org/packages/Herimanitra.Architecture.Analyzer) to enforce architecture and naming conventions in .NET solutions.

## Repository layout

| Folder | Purpose |
|--------|---------|
| [`Architecture.Analyzer/`](Architecture.Analyzer) | The analyzer library, packaged as a NuGet analyzer (`analyzers/dotnet/cs`). |
| [`Architecture.Analyzer.Tests/`](Architecture.Analyzer.Tests) | xUnit tests running each analyzer against in-memory Roslyn projects. |
| [`Architecture.Analyzer/build/`](Architecture.Analyzer/build) | MSBuild targets shipped with the package, used to compute the LOC budget inputs from `git`. |

The full user-facing documentation lives in the packaged README: [`Architecture.Analyzer/README.md`](Architecture.Analyzer/README.md).

## Installation

```shell
dotnet add package Herimanitra.Architecture.Analyzer
```

## Analyzers

| ID | Description |
|----|-------------|
| `ARCH001` | Model type placed in the wrong directory |
| `ARCH002` | File name must match the type name |
| `ARCH003` | Model type in a disallowed folder / missing provider contracts in a module |
| `ARCH004` | Provider implementation forbidden inside the module |
| `ARCH005` | DTO properties are not properly ordered |
| `ARCH006` | Lines-of-code budget exceeded (class) |
| `ARCH007` | Lines-of-code budget exceeded (method) |
| `ARCH008` | Lines-of-code budget exceeded (solution) |
| `ARCH009` | Lines-of-code budget exceeded (project) |

## Localization

Diagnostic titles and messages are localized. French is the neutral fallback; English translations ship in the `en` satellite assembly (`analyzers/dotnet/cs/en/Architecture.Analyzer.resources.dll`). Add a language by dropping a new `Resources.<culture>.resx` next to `Resources.resx` and packing the produced satellite in `Architecture.Analyzer.csproj`.

## Building and testing

```shell
dotnet build Architecture.Analyzers.slnx -c Release
dotnet test Architecture.Analyzer.Tests/Architecture.Analyzer.Tests.csproj -c Release
```

Building the analyzer project also produces the NuGet package under `Architecture.Analyzer/bin/Release/`.

## License

See [`Architecture.Analyzer/LICENSE.txt`](Architecture.Analyzer/LICENSE.txt).
