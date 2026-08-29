using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Architecture.Analyzer.Services.MethodOrderRule
{
    internal interface IMethodOrderRuleService
    {
        IEnumerable<ClassMethodOrderViolation> GetClassMethodOrderViolations(ClassDeclarationSyntax declaration, CancellationToken cancellationToken);
    }
}
