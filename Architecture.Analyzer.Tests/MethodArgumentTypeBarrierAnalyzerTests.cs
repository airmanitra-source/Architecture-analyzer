using Architecture.Analyzer;

namespace Architecture.Analyzer.Tests;

public sealed class MethodArgumentTypeBarrierAnalyzerTests
{
    private const string Barrier = "architecture_analyzer.method_argument_type_barrier";

    private static Dictionary<string, string> Config(string value) => new() { [Barrier] = value };

    private static async Task<int> CountAsync(string source, string? rule, string fileName = "Test.cs")
    {
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new MethodArgumentTypeBarrierAnalyzer(),
            fileName,
            rule is null ? null : Config(rule));

        return diagnostics.Count(diagnostic => diagnostic.Id == MethodArgumentTypeBarrierAnalyzer.DiagnosticId);
    }

    // --- Suffix matching (the canonical "*Module>*DataModel" case) ---

    [Fact]
    public async Task ReportsWhenModuleClassMethodHasForbiddenParameterType()
    {
        const string source = @"
public class CustomerDataModel { }
public class CustomerModule
{
    public void Register(CustomerDataModel model) { }
}";

        Assert.Equal(1, await CountAsync(source, "*Module>*DataModel"));
    }

    [Fact]
    public async Task DoesNotReportWhenRuleIsNotConfigured()
    {
        const string source = @"
public class CustomerDataModel { }
public class CustomerModule
{
    public void Register(CustomerDataModel model) { }
}";

        Assert.Equal(0, await CountAsync(source, null));
    }

    [Fact]
    public async Task DoesNotReportWhenDeclaringClassDoesNotMatch()
    {
        const string source = @"
public class CustomerDataModel { }
public class CustomerService
{
    public void Register(CustomerDataModel model) { }
}";

        Assert.Equal(0, await CountAsync(source, "*Module>*DataModel"));
    }

    [Fact]
    public async Task DoesNotReportWhenParameterTypeDoesNotMatch()
    {
        const string source = @"
public class CustomerModule
{
    public void Register(string name) { }
}";

        Assert.Equal(0, await CountAsync(source, "*Module>*DataModel"));
    }

    [Fact]
    public async Task DoesNotReportWhenSuffixOnlyPartiallyMatches()
    {
        // The parameter type ends with 'Model' but not 'DataModel'; the rule must not fire.
        const string source = @"
public class CustomerViewModel { }
public class CustomerModule
{
    public void Register(CustomerViewModel model) { }
}";

        Assert.Equal(0, await CountAsync(source, "*Module>*DataModel"));
    }

    // --- Prefix matching ("Prefix*") ---

    [Fact]
    public async Task ReportsWhenParameterTypeMatchesPrefixPattern()
    {
        const string source = @"
public class DataModelSnapshot { }
public class CustomerModule
{
    public void Register(DataModelSnapshot snapshot) { }
}";

        Assert.Equal(1, await CountAsync(source, "*Module>DataModel*"));
    }

    [Fact]
    public async Task DoesNotReportWhenParameterTypeDoesNotStartWithPrefix()
    {
        const string source = @"
public class CustomerDataModel { }
public class CustomerModule
{
    public void Register(CustomerDataModel model) { }
}";

        // "DataModel*" is a prefix pattern: CustomerDataModel starts with 'Customer', not 'DataModel'.
        Assert.Equal(0, await CountAsync(source, "*Module>DataModel*"));
    }

    [Fact]
    public async Task ReportsWhenClassMatchesPrefixPattern()
    {
        const string source = @"
public class CustomerDataModel { }
public class ModuleRegistry
{
    public void Register(CustomerDataModel model) { }
}";

        Assert.Equal(1, await CountAsync(source, "Module*>*DataModel"));
    }

    // --- Whole-name (exact) matching ---

    [Fact]
    public async Task ReportsWhenParameterTypeMatchesWholeName()
    {
        const string source = @"
public class DataModel { }
public class CustomerModule
{
    public void Register(DataModel model) { }
}";

        Assert.Equal(1, await CountAsync(source, "*Module>DataModel"));
    }

    [Fact]
    public async Task DoesNotReportWhenWholeNamePatternIsNotAnExactMatch()
    {
        const string source = @"
public class CustomerDataModel { }
public class CustomerModule
{
    public void Register(CustomerDataModel model) { }
}";

        // "DataModel" (no '*') is an exact match: CustomerDataModel is not exactly 'DataModel'.
        Assert.Equal(0, await CountAsync(source, "*Module>DataModel"));
    }

    [Fact]
    public async Task ReportsWhenClassMatchesWholeName()
    {
        const string source = @"
public class CustomerDataModel { }
public class Module
{
    public void Register(CustomerDataModel model) { }
}";

        Assert.Equal(1, await CountAsync(source, "Module>*DataModel"));
    }

    [Fact]
    public async Task DoesNotReportWhenClassWholeNamePatternIsNotAnExactMatch()
    {
        const string source = @"
public class CustomerDataModel { }
public class CustomerModule
{
    public void Register(CustomerDataModel model) { }
}";

        // "Module" (no '*') is an exact class match: CustomerModule is not exactly 'Module'.
        Assert.Equal(0, await CountAsync(source, "Module>*DataModel"));
    }

    // --- Substring ("*X*") matching ---

    [Fact]
    public async Task ReportsWhenParameterTypeContainsToken()
    {
        const string source = @"
public class MyDataSnapshot { }
public class CustomerModule
{
    public void Register(MyDataSnapshot snapshot) { }
}";

        Assert.Equal(1, await CountAsync(source, "*Module>*Data*"));
    }

    // --- Looking through arrays and generics ---

    [Fact]
    public async Task ReportsForForbiddenTypeAsGenericArgument()
    {
        const string source = @"
public class CustomerDataModel { }
public class Wrapper<T> { }
public class CustomerModule
{
    public void Register(Wrapper<CustomerDataModel> models) { }
}";

        Assert.Equal(1, await CountAsync(source, "*Module>*DataModel"));
    }

    [Fact]
    public async Task ReportsForForbiddenTypeAsArrayElement()
    {
        const string source = @"
public class CustomerDataModel { }
public class CustomerModule
{
    public void Register(CustomerDataModel[] models) { }
}";

        Assert.Equal(1, await CountAsync(source, "*Module>*DataModel"));
    }

    // --- Multiple parameters / multiple rules ---

    [Fact]
    public async Task ReportsOnceForEachForbiddenParameter()
    {
        const string source = @"
public class CustomerDataModel { }
public class OrderDataModel { }
public class CustomerModule
{
    public void Register(CustomerDataModel customer, OrderDataModel order, string label) { }
}";

        Assert.Equal(2, await CountAsync(source, "*Module>*DataModel"));
    }

    [Fact]
    public async Task SupportsMultipleSemicolonSeparatedRules()
    {
        const string source = @"
public class OrderEntity { }
public class BillingModule
{
    public void Charge(OrderEntity order) { }
}";

        Assert.Equal(1, await CountAsync(source, "*Module>*DataModel;*Module>*Entity"));
    }

    // --- The pattern targets nominal argument types, not placeholders ---

    [Fact]
    public async Task DoesNotReportForGenericMethodTypeParameter()
    {
        // 'TDataModel' is the method's own type parameter (a placeholder), not a
        // concrete argument type ending with 'DataModel'; the rule must not fire.
        const string source = @"
public class CustomerModule
{
    public void Register<TDataModel>(TDataModel item) { }
}";

        Assert.Equal(0, await CountAsync(source, "*Module>*DataModel"));
    }

    [Fact]
    public async Task DoesNotReportForUnresolvedParameterType()
    {
        // 'UndefinedDataModel' does not exist: the rule must not stack ARCH016 onto
        // code that does not compile yet.
        const string source = @"
public class CustomerModule
{
    public void Register(UndefinedDataModel model) { }
}";

        Assert.Equal(0, await CountAsync(source, "*Module>*DataModel"));
    }

    // --- Scope: classes only (interfaces and structs are excluded) ---

    [Fact]
    public async Task DoesNotReportForMethodDeclaredInInterface()
    {
        const string source = @"
public class ReportDataModel { }
public interface IReportModule
{
    void Save(ReportDataModel model);
}";

        Assert.Equal(0, await CountAsync(source, "*Module>*DataModel"));
    }

    [Fact]
    public async Task DoesNotReportForMethodDeclaredInStruct()
    {
        const string source = @"
public class PaymentDataModel { }
public struct PaymentModule
{
    public void Charge(PaymentDataModel model) { }
}";

        Assert.Equal(0, await CountAsync(source, "*Module>*DataModel"));
    }

    // --- The message names the declared parameter type, not the nested match ---

    [Fact]
    public async Task MessageNamesTheDeclaredParameterTypeForWrappedTypes()
    {
        const string source = @"
public class CustomerDataModel { }
public class Wrapper<T> { }
public class CustomerModule
{
    public void Register(Wrapper<CustomerDataModel> models) { }
}";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new MethodArgumentTypeBarrierAnalyzer(),
            "CustomerModule.cs",
            Config("*Module>*DataModel"));

        var diagnostic = Assert.Single(diagnostics, d => d.Id == MethodArgumentTypeBarrierAnalyzer.DiagnosticId);
        Assert.Contains("Wrapper<CustomerDataModel>", diagnostic.GetMessage());
    }
}
