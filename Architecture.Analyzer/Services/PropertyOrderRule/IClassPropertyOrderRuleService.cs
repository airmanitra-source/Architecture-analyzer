using System.Collections.Generic;
using System.Threading;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Architecture.Analyzer.Services.PropertyOrderRule;

internal interface IClassPropertyOrderRuleService
{
    List<ClassPropertyOrderViolation> GetClassPropertyOrderViolations(ClassDeclarationSyntax declaration, CancellationToken cancellationToken);
}