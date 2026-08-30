using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.NameLengthRule;

internal interface INameLengthRuleService
{
    int? GetMinVariableNameLength(AnalyzerConfigOptions options);

    int? GetMinClassNameLength(AnalyzerConfigOptions options);

    NameLengthViolation? GetVariableNameViolation(SyntaxToken identifier, int minLength);

    NameLengthViolation? GetClassNameViolation(ClassDeclarationSyntax declaration, int minLength);
}
