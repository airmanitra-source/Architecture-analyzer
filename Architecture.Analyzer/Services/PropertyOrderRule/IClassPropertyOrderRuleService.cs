using System.Collections.Generic;
using System.Threading;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Architecture.Analyzer.Services.PropertyOrderRule;

internal interface IClassPropertyOrderRuleService
{
    List<ClassPropertyOrderViolation> Analyze(ClassDeclarationSyntax declaration, CancellationToken cancellationToken);
}