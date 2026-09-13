using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.ComplexityRatchetRule;

internal sealed class ComplexityRatchetRuleService : IComplexityRatchetRuleService
{
    private const string AnalyzerConfigPrefix = "architecture_analyzer.";
    private const string AllowedIncreaseOption = AnalyzerConfigPrefix + "complexity_ratchet_allowed_increase";

    // Marks an AdditionalFile as the HEAD version of a source file, produced by the shipped MSBuild
    // target. Its value is the original path, kept for readability of the build log.
    private const string BaselineMetadata = "build_metadata.AdditionalFiles.ArchitectureBaselineFor";

    public int GetAllowedIncrease(AnalyzerConfigOptions options)
        => options.TryGetValue(AllowedIncreaseOption, out var value)
           && !string.IsNullOrWhiteSpace(value)
           && int.TryParse(value.Trim(), out var parsed)
           && parsed >= 0
            ? parsed
            : 0;

    public Dictionary<string, int> BuildBaseline(
        ImmutableArray<AdditionalText> additionalFiles,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken)
    {
        var baseline = new Dictionary<string, int>(System.StringComparer.Ordinal);

        foreach (var file in additionalFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!optionsProvider.GetOptions(file).TryGetValue(BaselineMetadata, out var origin)
                || string.IsNullOrWhiteSpace(origin))
            {
                continue; // an AdditionalFile that has nothing to do with this rule.
            }

            var text = file.GetText(cancellationToken);
            if (text is null)
            {
                continue;
            }

            var root = CSharpSyntaxTree.ParseText(text.ToString(), cancellationToken: cancellationToken).GetRoot(cancellationToken);
            foreach (var method in root.DescendantNodes())
            {
                if (method is not MethodDeclarationSyntax declaration)
                {
                    continue;
                }

                var key = BuildKey(declaration);
                var complexity = ComputeComplexity(declaration);

                // Overloads can collide on the key. Keeping the highest baseline is the conservative
                // choice: it can only remove reports, never invent one.
                if (!baseline.TryGetValue(key, out var existing) || complexity > existing)
                {
                    baseline[key] = complexity;
                }
            }
        }

        return baseline;
    }

    // Cyclomatic complexity: one, plus one per decision point. 'else' adds nothing — it is part of
    // the 'if' that was already counted.
    public int ComputeComplexity(MethodDeclarationSyntax method)
    {
        SyntaxNode? body = method.Body;
        if (body is null)
        {
            body = method.ExpressionBody;
        }

        if (body is null)
        {
            return 0; // abstract or interface declaration.
        }

        var complexity = 1;
        foreach (var node in body.DescendantNodesAndSelf())
        {
            switch (node.Kind())
            {
                case SyntaxKind.IfStatement:
                case SyntaxKind.WhileStatement:
                case SyntaxKind.DoStatement:
                case SyntaxKind.ForStatement:
                case SyntaxKind.ForEachStatement:
                case SyntaxKind.ForEachVariableStatement:
                case SyntaxKind.CaseSwitchLabel:
                case SyntaxKind.CasePatternSwitchLabel:
                case SyntaxKind.SwitchExpressionArm:
                case SyntaxKind.CatchClause:
                case SyntaxKind.WhenClause:
                case SyntaxKind.ConditionalExpression:
                case SyntaxKind.LogicalAndExpression:
                case SyntaxKind.LogicalOrExpression:
                case SyntaxKind.CoalesceExpression:
                case SyntaxKind.CoalesceAssignmentExpression:
                    complexity++;
                    break;
            }
        }

        return complexity;
    }

    public ComplexityRatchetViolation? Check(MethodDeclarationSyntax method, Dictionary<string, int> baseline, int allowedIncrease)
    {
        // A method absent from the baseline is new: there is nothing it could have degraded.
        if (!baseline.TryGetValue(BuildKey(method), out var before))
        {
            return null;
        }

        var after = ComputeComplexity(method);
        if (after <= before + allowedIncrease)
        {
            return null;
        }

        return new ComplexityRatchetViolation(
            BuildDisplayName(method),
            before,
            after,
            method.Identifier.GetLocation());
    }

    // Type + method + parameter count: stable when the method moves inside the file, which happens
    // constantly, unlike a line number.
    private static string BuildKey(MethodDeclarationSyntax method)
        => BuildDisplayName(method) + "#" + method.ParameterList.Parameters.Count.ToString();

    private static string BuildDisplayName(MethodDeclarationSyntax method)
    {
        var name = method.Identifier.ValueText;
        return method.Parent is TypeDeclarationSyntax type
            ? type.Identifier.ValueText + "." + name
            : name;
    }
}
