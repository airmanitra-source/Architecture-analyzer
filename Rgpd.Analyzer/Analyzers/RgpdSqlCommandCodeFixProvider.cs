using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rgpd.Analyzer.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RgpdSqlCommandCodeFixProvider)), Shared]
public sealed class RgpdSqlCommandCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RgpdSqlCommandAnalyzer.DiagnosticId);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics.First();
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        if (node is not ObjectCreationExpressionSyntax creationExpression)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Use SqlCommandFactory.CreateFilterCommand",
                createChangedDocument: cancellationToken => ReplaceWithFactoryCallAsync(context.Document, root, creationExpression, cancellationToken),
                equivalenceKey: "UseSqlCommandFactory"),
            diagnostic);
    }

    private static Task<Document> ReplaceWithFactoryCallAsync(
        Document document,
        SyntaxNode root,
        ObjectCreationExpressionSyntax creationExpression,
        CancellationToken cancellationToken)
    {
        var arguments = creationExpression.ArgumentList?.Arguments ?? default;
        var hasTwoArguments = arguments.Count >= 2;
        var hasOneArgument = arguments.Count >= 1;

        var connectionArgument = hasTwoArguments
            ? arguments[1].Expression
            : SyntaxFactory.IdentifierName("connection");

        var sqlArgument = hasOneArgument
            ? arguments[0].Expression
            : SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(string.Empty));

        var replacement = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("_sqlCommandFactory"),
                    SyntaxFactory.IdentifierName("CreateFilterCommand")))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(new[]
                    {
                        SyntaxFactory.Argument(connectionArgument),
                        SyntaxFactory.Argument(sqlArgument)
                    })));

        var newRoot = root.ReplaceNode(creationExpression, replacement.WithTriviaFrom(creationExpression));
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
