using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Architecture.Analyzer.Services.MethodOrderRule
{
    internal class MethodOrderRuleService : IMethodOrderRuleService
    {
        public IEnumerable<ClassMethodOrderViolation> GetClassMethodOrderViolations(ClassDeclarationSyntax declaration, CancellationToken cancellationToken)
        {
            var violations = new List<ClassMethodOrderViolation>();
            string? previousMethodName = null;

            foreach (var method in declaration.Members.OfType<MethodDeclarationSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var currentMethodName = method.Identifier.ValueText;
                if (previousMethodName is not null
                    && string.Compare(currentMethodName, previousMethodName, StringComparison.Ordinal) < 0)
                {
                    violations.Add(new ClassMethodOrderViolation(
                        declaration.Identifier.ValueText,
                        previousMethodName,
                        currentMethodName,
                        method.Identifier.GetLocation()));
                }

                previousMethodName = currentMethodName;
            }

            return violations;
        }
    }
}
