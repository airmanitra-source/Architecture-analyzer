using System.Globalization;
using Architecture.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Tests;

public sealed class LocalizationTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");

    [Fact]
    public async Task ClassPropertyOrderAnalyzer_LocalizesTitleAndMessage()
    {
        const string source = "public class CustomerBusinessModel { public string Name { get; set; } public string Age { get; set; } }";

        var diagnostic = await GetFirstDiagnosticAsync(
            source,
            new ClassPropertyOrderAnalyzer(),
            ClassPropertyOrderAnalyzer.DiagnosticId);

        AssertTitle(diagnostic, "propriétés du DTO", "DTO properties");
        AssertMessage(diagnostic, "doit être placée avant", "must be placed before");
    }

    [Fact]
    public async Task ModelDirectoryAnalyzer_LocalizesDirectoryTitleAndMessage()
    {
        const string source = "public class CustomerBusinessModel { }";

        var diagnostic = await GetFirstDiagnosticAsync(
            source,
            new ModelDirectoryAnalyzer(),
            ModelDirectoryAnalyzer.DiagnosticId,
            fileName: "Features/CustomerBusinessModel.cs");

        AssertTitle(diagnostic, "mauvais dossier", "wrong folder");
        AssertMessage(diagnostic, "doit être dans le dossier", "must be located in folder");
    }

    [Fact]
    public async Task ModelDirectoryAnalyzer_LocalizesFileNameTitleAndMessage()
    {
        const string source = "public class CustomerBusinessModel { }";

        var diagnostic = await GetFirstDiagnosticAsync(
            source,
            new ModelDirectoryAnalyzer(),
            ModelDirectoryAnalyzer.FileNameDiagnosticId,
            fileName: "Business/Models/Customer.cs");

        AssertTitle(diagnostic, "nom du fichier doit correspondre", "File name must match");
        AssertMessage(diagnostic, "doit être déclaré dans un fichier nommé", "must be declared in a file named");
    }

    [Fact]
    public async Task ModelDirectoryAnalyzer_LocalizesFolderAllowedTitleAndMessage()
    {
        const string source = "public class CustomerDataModel { }";

        var diagnostic = await GetFirstDiagnosticAsync(
            source,
            new ModelDirectoryAnalyzer(),
            ModelDirectoryAnalyzer.FolderAllowedDiagnosticId,
            fileName: "Models/Business/CustomerDataModel.cs",
            analyzerConfigOptions: new Dictionary<string, string>
            {
                ["architecture_analyzer.model_folder_allowed_suffixes"] = "Models/Business=BusinessModel;Models/Data=DataModel",
            });

        AssertTitle(diagnostic, "dossier non autorisé", "disallowed folder");
        AssertMessage(diagnostic, "n'est pas autorisé dans le dossier", "is not allowed in folder");
    }

    [Fact]
    public async Task ModuleProviderAnalyzer_LocalizesMissingContractsTitleAndMessage()
    {
        var diagnostic = await GetFirstDiagnosticFromDocumentsAsync(
            [("Class1.cs", "public class Class1 { }")],
            new ModuleProviderAnalyzer(),
            ModuleProviderAnalyzer.MissingProviderContractsDiagnosticId,
            projectName: "HR.Module");

        AssertTitle(diagnostic, "Contrats providers manquants", "Missing provider contracts");
        AssertMessage(diagnostic, "doit contenir le dossier", "must contain the");
    }

    [Fact]
    public async Task ModuleProviderAnalyzer_LocalizesLocalImplementationTitleAndMessage()
    {
        var diagnostic = await GetFirstDiagnosticFromDocumentsAsync(
            [
                ("Models/Data/Providers/IEmployeeProvider.cs", "namespace HR.Module.Models.Data.Providers { public interface IEmployeeProvider { } }"),
                ("Services/EmployeeProvider.cs", "namespace HR.Module.Services { public sealed class EmployeeProvider : Models.Data.Providers.IEmployeeProvider { } }")
            ],
            new ModuleProviderAnalyzer(),
            ModuleProviderAnalyzer.LocalProviderImplementationDiagnosticId,
            projectName: "HR.Module");

        AssertTitle(diagnostic, "Implémentation provider interdite", "Provider implementation is not allowed");
        AssertMessage(diagnostic, "ne doit pas implémenter l'interface provider", "must not implement the provider interface");
    }

    [Fact]
    public async Task LocBudgetAnalyzer_LocalizesClassTitleAndMessage()
    {
        var members = string.Join("\n", Enumerable.Range(1, 30).Select(index => $"    public int Value{index} {{ get; set; }}"));
        var source = $"namespace Architecture.Rules.Demo;\n\npublic class TestBusiness\n{{\n{members}\n}}\n";

        var diagnostic = await GetFirstDiagnosticAsync(
            source,
            new LocBudgetAnalyzer(),
            LocBudgetAnalyzer.ClassDiagnosticId,
            fileName: "TestBusiness.cs",
            analyzerConfigOptions: new Dictionary<string, string> { ["architecture_analyzer.loc_max_lines_per_class"] = "20" });

        AssertTitle(diagnostic, "La classe dépasse le budget", "Class exceeds the added-lines budget");
        AssertMessage(diagnostic, "ajoute", "adds");
        AssertMessage(diagnostic, "dépasse le budget", "exceeds the");
    }

    [Fact]
    public async Task LocBudgetAnalyzer_LocalizesMethodTitleAndMessage()
    {
        var statements = string.Join("\n", Enumerable.Range(1, 30).Select(index => $"        var value{index} = {index};"));
        var source = $"namespace Architecture.Rules.Demo;\n\npublic class TestBusiness\n{{\n    public void Run()\n    {{\n{statements}\n    }}\n}}\n";

        var diagnostic = await GetFirstDiagnosticAsync(
            source,
            new LocBudgetAnalyzer(),
            LocBudgetAnalyzer.MethodDiagnosticId,
            fileName: "TestBusiness.cs",
            analyzerConfigOptions: new Dictionary<string, string> { ["architecture_analyzer.loc_max_lines_per_method"] = "20" });

        AssertTitle(diagnostic, "La méthode dépasse le budget", "Method exceeds the added-lines budget");
        AssertMessage(diagnostic, "ajoute", "adds");
    }

    [Fact]
    public async Task LocBudgetAnalyzer_LocalizesProjectTitleAndMessage()
    {
        const string tinySource = "namespace Architecture.Rules.Demo;\n\npublic class TestBusiness\n{\n    public void Run()\n    {\n    }\n}\n";

        var diagnostic = await GetFirstDiagnosticAsync(
            tinySource,
            new LocBudgetAnalyzer(),
            LocBudgetAnalyzer.ProjectDiagnosticId,
            fileName: "TestBusiness.cs",
            analyzerConfigOptions: new Dictionary<string, string>
            {
                ["architecture_analyzer.loc_budget_percent_project"] = "2",
                ["build_property.ArchitectureLocProjectBaselineLines"] = "1000",
                ["build_property.ArchitectureLocProjectAddedLines"] = "30",
            });

        AssertTitle(diagnostic, "Le projet dépasse le budget", "Project exceeds the added-lines budget");
        AssertMessage(diagnostic, "Le projet", "Project");
    }

    [Fact]
    public async Task LocBudgetAnalyzer_LocalizesSolutionTitleAndMessage()
    {
        const string tinySource = "namespace Architecture.Rules.Demo;\n\npublic class TestBusiness\n{\n    public void Run()\n    {\n    }\n}\n";

        var diagnostic = await GetFirstDiagnosticAsync(
            tinySource,
            new LocBudgetAnalyzer(),
            LocBudgetAnalyzer.SolutionDiagnosticId,
            fileName: "TestBusiness.cs",
            analyzerConfigOptions: new Dictionary<string, string>
            {
                ["architecture_analyzer.loc_budget_percent_global"] = "2",
                ["build_property.ArchitectureLocSolutionBaselineLines"] = "1000",
                ["build_property.ArchitectureLocSolutionAddedLines"] = "30",
            });

        AssertTitle(diagnostic, "La solution dépasse le budget global", "Solution exceeds the global added-lines budget");
        AssertMessage(diagnostic, "budget global", "global");
    }

    [Fact]
    public async Task ClassMethodOrderAnalyzer_LocalizesTitleAndMessage()
    {
        const string source = "public class Customer { public void SaveCustomer() { } public void DeleteCustomer() { } }";

        var diagnostic = await GetFirstDiagnosticAsync(
            source,
            new ClassMethodOrderAnalyzer(),
            ClassMethodOrderAnalyzer.DiagnosticId);

        AssertTitle(diagnostic, "méthodes de la classe", "Class methods");
        AssertMessage(diagnostic, "doit être placée avant", "must be placed before");
    }

    [Fact]
    public async Task RequiredProjectFolderAnalyzer_LocalizesTitleAndMessage()
    {
        var diagnostic = await GetFirstDiagnosticFromDocumentsAsync(
            [("Class1.cs", "public class Class1 { }")],
            new RequiredProjectFolderAnalyzer(),
            RequiredProjectFolderAnalyzer.DiagnosticId,
            projectName: "HR.Infrastructure",
            analyzerConfigOptions: new Dictionary<string, string>
            {
                ["architecture_analyzer.required_project_folders"] = "HR.Infrastructure=Models/Entities",
            });

        AssertTitle(diagnostic, "Dossier requis manquant", "Missing required folder");
        AssertMessage(diagnostic, "doit contenir le dossier", "must contain the folder");
    }

    [Fact]
    public async Task NameLengthAnalyzer_LocalizesVariableTitleAndMessage()
    {
        const string source = "public class Customer { public void Run() { int a = 1; } }";

        var diagnostic = await GetFirstDiagnosticAsync(
            source,
            new NameLengthAnalyzer(),
            NameLengthAnalyzer.VariableDiagnosticId,
            fileName: "Customer.cs",
            analyzerConfigOptions: new Dictionary<string, string> { ["architecture_analyzer.min_variable_name_length"] = "3" });

        AssertTitle(diagnostic, "nom de la variable est trop court", "Variable name is too short");
        AssertMessage(diagnostic, "au moins", "at least");
    }

    [Fact]
    public async Task NameLengthAnalyzer_LocalizesClassTitleAndMessage()
    {
        const string source = "public class Ab { }";

        var diagnostic = await GetFirstDiagnosticAsync(
            source,
            new NameLengthAnalyzer(),
            NameLengthAnalyzer.ClassDiagnosticId,
            fileName: "Ab.cs",
            analyzerConfigOptions: new Dictionary<string, string> { ["architecture_analyzer.min_class_name_length"] = "3" });

        AssertTitle(diagnostic, "nom de la classe est trop court", "Class name is too short");
        AssertMessage(diagnostic, "au moins", "at least");
    }

    [Fact]
    public async Task NameLengthAnalyzer_LocalizesMethodTitleAndMessage()
    {
        const string source = "public class Customer { public void Go() { } }";

        var diagnostic = await GetFirstDiagnosticAsync(
            source,
            new NameLengthAnalyzer(),
            NameLengthAnalyzer.MethodDiagnosticId,
            fileName: "Customer.cs",
            analyzerConfigOptions: new Dictionary<string, string> { ["architecture_analyzer.min_method_name_length"] = "4" });

        AssertTitle(diagnostic, "nom de la méthode est trop court", "Method name is too short");
        AssertMessage(diagnostic, "au moins", "at least");
    }

    private static void AssertTitle(Diagnostic diagnostic, string frenchFragment, string englishFragment)
    {
        var frenchTitle = diagnostic.Descriptor.Title.ToString(French);
        var englishTitle = diagnostic.Descriptor.Title.ToString(English);

        Assert.Contains(frenchFragment, frenchTitle);
        Assert.Contains(englishFragment, englishTitle);
        Assert.NotEqual(frenchTitle, englishTitle);
    }

    private static void AssertMessage(Diagnostic diagnostic, string frenchFragment, string englishFragment)
    {
        var frenchMessage = diagnostic.GetMessage(French);
        var englishMessage = diagnostic.GetMessage(English);

        Assert.Contains(frenchFragment, frenchMessage);
        Assert.Contains(englishFragment, englishMessage);
        Assert.NotEqual(frenchMessage, englishMessage);
    }

    private static async Task<Diagnostic> GetFirstDiagnosticAsync(
        string source,
        DiagnosticAnalyzer analyzer,
        string diagnosticId,
        string fileName = "Test.cs",
        IReadOnlyDictionary<string, string>? analyzerConfigOptions = null)
    {
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            analyzer,
            fileName,
            analyzerConfigOptions);
        return diagnostics.First(d => d.Id == diagnosticId);
    }

    private static async Task<Diagnostic> GetFirstDiagnosticFromDocumentsAsync(
        IReadOnlyList<(string fileName, string source)> documents,
        DiagnosticAnalyzer analyzer,
        string diagnosticId,
        string projectName,
        IReadOnlyDictionary<string, string>? analyzerConfigOptions = null)
    {
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            documents,
            analyzer,
            projectName,
            analyzerConfigOptions);
        return diagnostics.First(d => d.Id == diagnosticId);
    }
}
