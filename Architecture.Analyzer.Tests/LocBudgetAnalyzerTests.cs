using System.Globalization;
using Architecture.Analyzer;
using LibGit2Sharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Tests;

public sealed class LocBudgetAnalyzerTests
{
    [Fact]
    public async Task DoesNotReportWhenRepositoryHasNoChanges()
    {
        await using var fixture = await LocBudgetRepositoryFixture.CreateAsync(
            baselineFiles: new Dictionary<string, string>
            {
                ["Seed.cs"] = LocBudgetRepositoryFixture.CreateSeedSource(1000),
                ["TestBusiness.cs"] = "namespace Architecture.Rules.Demo;\n\npublic class TestBusiness\n{\n}\n"
            },
            currentFiles: new Dictionary<string, string>
            {
                ["Seed.cs"] = LocBudgetRepositoryFixture.CreateSeedSource(1000),
                ["TestBusiness.cs"] = "namespace Architecture.Rules.Demo;\n\npublic class TestBusiness\n{\n}\n"
            });

        var diagnostics = await fixture.AnalyzeAsync(
            new LocBudgetAnalyzer(),
            new Dictionary<string, string>
            {
                ["architecture_analyzer.loc_budget_percent_project"] = "2",
                ["architecture_analyzer.loc_budget_percent_global"] = "2",
                ["architecture_analyzer.loc_max_lines_per_class"] = "20",
                ["architecture_analyzer.loc_max_lines_per_method"] = "20"
            });

        Assert.DoesNotContain(diagnostics, diagnostic =>
            diagnostic.Id is LocBudgetAnalyzer.ClassDiagnosticId
                or LocBudgetAnalyzer.MethodDiagnosticId
                or LocBudgetAnalyzer.ProjectDiagnosticId
                or LocBudgetAnalyzer.SolutionDiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenClassAddsTooManyLines()
    {
        var currentClass = string.Join("\n", Enumerable.Range(1, 30).Select(index => $"    public int Value{index} {{ get; set; }}"));
        var currentSource = $"namespace Architecture.Rules.Demo;\n\npublic class TestBusiness\n{{\n{currentClass}\n}}\n";

        await using var fixture = await LocBudgetRepositoryFixture.CreateAsync(
            baselineFiles: new Dictionary<string, string>
            {
                ["Seed.cs"] = LocBudgetRepositoryFixture.CreateSeedSource(1000),
                ["TestBusiness.cs"] = "namespace Architecture.Rules.Demo;\n\npublic class TestBusiness\n{\n}\n"
            },
            currentFiles: new Dictionary<string, string>
            {
                ["Seed.cs"] = LocBudgetRepositoryFixture.CreateSeedSource(1000),
                ["TestBusiness.cs"] = currentSource
            });

        var diagnostics = await fixture.AnalyzeAsync(
            new LocBudgetAnalyzer(),
            new Dictionary<string, string>
            {
                ["architecture_analyzer.loc_max_lines_per_class"] = "20"
            });

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == LocBudgetAnalyzer.ClassDiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenMethodAddsTooManyLines()
    {
        var currentStatements = string.Join("\n", Enumerable.Range(1, 30).Select(index => $"        var value{index} = {index};"));
        var currentSource = $"namespace Architecture.Rules.Demo;\n\npublic class TestBusiness\n{{\n    public void Run()\n    {{\n{currentStatements}\n    }}\n}}\n";

        await using var fixture = await LocBudgetRepositoryFixture.CreateAsync(
            baselineFiles: new Dictionary<string, string>
            {
                ["Seed.cs"] = LocBudgetRepositoryFixture.CreateSeedSource(1000),
                ["TestBusiness.cs"] = "namespace Architecture.Rules.Demo;\n\npublic class TestBusiness\n{\n    public void Run()\n    {\n    }\n}\n"
            },
            currentFiles: new Dictionary<string, string>
            {
                ["Seed.cs"] = LocBudgetRepositoryFixture.CreateSeedSource(1000),
                ["TestBusiness.cs"] = currentSource
            });

        var diagnostics = await fixture.AnalyzeAsync(
            new LocBudgetAnalyzer(),
            new Dictionary<string, string>
            {
                ["architecture_analyzer.loc_max_lines_per_method"] = "20"
            });

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == LocBudgetAnalyzer.MethodDiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnosticOnActualHrModuleFile()
    {
        var source = await File.ReadAllTextAsync(@"D:\rgpd\HR.Module\HrModule.cs");

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new LocBudgetAnalyzer(),
            @"D:\rgpd\HR.Module\HrModule.cs");

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == LocBudgetAnalyzer.MethodDiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenMethodExceedsHardLimitWithoutConfiguration()
    {
        var statements = string.Join("\n", Enumerable.Range(1, 30).Select(index => $"        var value{index} = {index};"));
        var source = $"namespace Architecture.Rules.Demo;\n\npublic class TestBusiness\n{{\n    public void Run()\n    {{\n{statements}\n    }}\n}}\n";

        await using var fixture = await LocBudgetRepositoryFixture.CreateAsync(
            baselineFiles: new Dictionary<string, string>
            {
                ["Seed.cs"] = LocBudgetRepositoryFixture.CreateSeedSource(1000),
                ["TestBusiness.cs"] = source
            },
            currentFiles: new Dictionary<string, string>
            {
                ["Seed.cs"] = LocBudgetRepositoryFixture.CreateSeedSource(1000),
                ["TestBusiness.cs"] = source
            });

        var diagnostics = await fixture.AnalyzeAsync(new LocBudgetAnalyzer());

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == LocBudgetAnalyzer.MethodDiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenMethodExceedsHardLimitEvenWithoutGitDelta()
    {
        var statements = string.Join("\n", Enumerable.Range(1, 30).Select(index => $"        var value{index} = {index};"));
        var source = $"namespace Architecture.Rules.Demo;\n\npublic class TestBusiness\n{{\n    public void Run()\n    {{\n{statements}\n    }}\n}}\n";

        await using var fixture = await LocBudgetRepositoryFixture.CreateAsync(
            baselineFiles: new Dictionary<string, string>
            {
                ["Seed.cs"] = LocBudgetRepositoryFixture.CreateSeedSource(1000),
                ["TestBusiness.cs"] = source
            },
            currentFiles: new Dictionary<string, string>
            {
                ["Seed.cs"] = LocBudgetRepositoryFixture.CreateSeedSource(1000),
                ["TestBusiness.cs"] = source
            });

        var diagnostics = await fixture.AnalyzeAsync(
            new LocBudgetAnalyzer(),
            new Dictionary<string, string>
            {
                ["architecture_analyzer.loc_max_lines_per_method"] = "20"
            });

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == LocBudgetAnalyzer.MethodDiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenProjectAddsTooManyLines()
    {
        var currentExtra = string.Join("\n", Enumerable.Range(1, 25).Select(index => $"public class Generated{index} {{ }}"));

        await using var fixture = await LocBudgetRepositoryFixture.CreateAsync(
            baselineFiles: new Dictionary<string, string>
            {
                ["Seed.cs"] = LocBudgetRepositoryFixture.CreateSeedSource(1000),
                ["TestBusiness.cs"] = "namespace Architecture.Rules.Demo;\n\npublic class TestBusiness\n{\n}\n"
            },
            currentFiles: new Dictionary<string, string>
            {
                ["Seed.cs"] = LocBudgetRepositoryFixture.CreateSeedSource(1000),
                ["TestBusiness.cs"] = "namespace Architecture.Rules.Demo;\n\npublic class TestBusiness\n{\n}\n",
                ["Generated.cs"] = currentExtra
            });

        var diagnostics = await fixture.AnalyzeAsync(
            new LocBudgetAnalyzer(),
            new Dictionary<string, string>
            {
                ["architecture_analyzer.loc_budget_percent_project"] = "2"
            });

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == LocBudgetAnalyzer.ProjectDiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenGlobalBudgetIsLowerThanProjectBudget()
    {
        var currentExtra = string.Join("\n", Enumerable.Range(1, 25).Select(index => $"public class Generated{index} {{ }}"));

        await using var fixture = await LocBudgetRepositoryFixture.CreateAsync(
            baselineFiles: new Dictionary<string, string>
            {
                ["Seed.cs"] = LocBudgetRepositoryFixture.CreateSeedSource(1000),
                ["TestBusiness.cs"] = "namespace Architecture.Rules.Demo;\n\npublic class TestBusiness\n{\n}\n"
            },
            currentFiles: new Dictionary<string, string>
            {
                ["Seed.cs"] = LocBudgetRepositoryFixture.CreateSeedSource(1000),
                ["TestBusiness.cs"] = "namespace Architecture.Rules.Demo;\n\npublic class TestBusiness\n{\n}\n",
                ["Generated.cs"] = currentExtra
            });

        var diagnostics = await fixture.AnalyzeAsync(
            new LocBudgetAnalyzer(),
            new Dictionary<string, string>
            {
                ["architecture_analyzer.loc_budget_percent_project"] = "3",
                ["architecture_analyzer.loc_budget_percent_global"] = "2"
            });

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == LocBudgetAnalyzer.ProjectDiagnosticId);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == LocBudgetAnalyzer.SolutionDiagnosticId);
    }

    private sealed class LocBudgetRepositoryFixture : IAsyncDisposable
    {
        private readonly string _rootDirectory;

        private LocBudgetRepositoryFixture(string rootDirectory)
        {
            _rootDirectory = rootDirectory;
        }

        public static async Task<LocBudgetRepositoryFixture> CreateAsync(
            IReadOnlyDictionary<string, string> baselineFiles,
            IReadOnlyDictionary<string, string> currentFiles)
        {
            var rootDirectory = Path.Combine(Path.GetTempPath(), "rgpd-loc-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(rootDirectory);
            Repository.Init(rootDirectory);

            using (var repository = new Repository(rootDirectory))
            {
                foreach (var (relativePath, content) in baselineFiles)
                {
                    var fullPath = Path.Combine(rootDirectory, relativePath);
                    var directory = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    await File.WriteAllTextAsync(fullPath, content);
                }

                Commands.Stage(repository, "*");
                repository.Commit(
                    "baseline",
                    new Signature("Copilot", "copilot@example.com", DateTimeOffset.UtcNow),
                    new Signature("Copilot", "copilot@example.com", DateTimeOffset.UtcNow));
            }

            foreach (var (relativePath, content) in currentFiles)
            {
                var fullPath = Path.Combine(rootDirectory, relativePath);
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(fullPath, content);
            }

            return new LocBudgetRepositoryFixture(rootDirectory);
        }

        public async Task<List<Diagnostic>> AnalyzeAsync(
            DiagnosticAnalyzer analyzer,
            IReadOnlyDictionary<string, string>? analyzerConfigOptions = null)
        {
            var documents = new List<(string filePath, string source)>();
            foreach (var filePath in Directory.GetFiles(_rootDirectory, "*.cs", SearchOption.AllDirectories))
            {
                documents.Add((filePath, await File.ReadAllTextAsync(filePath)));
            }

            return await ArchitectureAnalyzerTestRunner.AnalyzeAsync(documents, analyzer, "Architecture.Rules.Demo", analyzerConfigOptions);
        }

        public ValueTask DisposeAsync()
        {
            try
            {
                if (Directory.Exists(_rootDirectory))
                {
                    Directory.Delete(_rootDirectory, true);
                }
            }
            catch
            {
            }

            return ValueTask.CompletedTask;
        }

        public static string CreateSeedSource(int lines)
            => string.Join("\n", Enumerable.Range(1, lines).Select(index => $"public sealed class Seed{index} {{ }}"));
    }
}