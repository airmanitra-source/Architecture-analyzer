using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Architecture.Analyzer.Services.TestLinkRule;

internal sealed class TestLinkRuleService : ITestLinkRuleService
{
    private const string Prefix = "architecture_analyzer.";
    private const string MinAddedLinesOption = Prefix + "test_link_min_added_lines";
    private const string ExcludePathsOption = Prefix + "test_link_exclude_paths";
    private const string TestAttributeNamesOption = Prefix + "test_attribute_names";
    private const string AssertionPatternsOption = Prefix + "test_assertion_patterns";

    private const string ChangedFilesMetadata = "build_metadata.AdditionalFiles.ArchitectureChangedFiles";
    private const string BaselineMetadata = "build_metadata.AdditionalFiles.ArchitectureBaselineFor";
    private const string ChangedTestFileMetadata = "build_metadata.AdditionalFiles.ArchitectureChangedTestFile";

    private const string DefaultTestAttributes = "Fact;Theory;Test;TestCase;TestCaseSource;TestMethod;DataTestMethod";
    private const string DefaultAssertionPatterns = "Assert;Should;Verify;Received;DidNotReceive;Expect;Throws";

    public TestLinkSettings GetSettings(AnalyzerConfigOptions options)
        => new(
            GetInt(options, MinAddedLinesOption, 0),
            GetList(options, ExcludePathsOption, string.Empty),
            GetList(options, TestAttributeNamesOption, DefaultTestAttributes),
            GetList(options, AssertionPatternsOption, DefaultAssertionPatterns));

    public HashSet<string> ReadChangedFiles(
        ImmutableArray<AdditionalText> additionalFiles,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken)
    {
        var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in additionalFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!optionsProvider.GetOptions(file).TryGetValue(ChangedFilesMetadata, out var marker)
                || !string.Equals(marker, "true", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = file.GetText(cancellationToken);
            if (text is null)
            {
                continue;
            }

            foreach (var line in text.Lines)
            {
                var path = line.ToString().Trim();
                if (path.Length > 0)
                {
                    changed.Add(path);
                }
            }
        }

        return changed;
    }

    public HashSet<string> ReadTestFilePaths(
        ImmutableArray<AdditionalText> additionalFiles,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in additionalFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (optionsProvider.GetOptions(file).TryGetValue(ChangedTestFileMetadata, out var marker)
                && string.Equals(marker, "true", StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(file.Path);
            }
        }

        return paths;
    }

    public Dictionary<(string Type, string Member, int Arity), (int Hash, int Lines)> BuildHeadMembers(
        ImmutableArray<AdditionalText> additionalFiles,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken)
    {
        var members = new Dictionary<(string, string, int), (int, int)>();
        foreach (var file in additionalFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!optionsProvider.GetOptions(file).TryGetValue(BaselineMetadata, out var origin)
                || string.IsNullOrWhiteSpace(origin))
            {
                continue;
            }

            var text = file.GetText(cancellationToken);
            if (text is null)
            {
                continue;
            }

            var root = CSharpSyntaxTree.ParseText(text, cancellationToken: cancellationToken).GetRoot(cancellationToken);
            foreach (var node in root.DescendantNodes())
            {
                if (!TryBuildKey(node, out var key))
                {
                    continue;
                }

                var body = GetBody(node);
                if (body is null)
                {
                    continue;
                }

                var analysis = AnalyzeBody(body, text);

                // Overloads collide on the key; keep the largest so the comparison can only ever remove
                // a report, never invent one.
                if (!members.TryGetValue(key, out var existing) || analysis.Lines > existing.Item2)
                {
                    members[key] = analysis;
                }
            }
        }

        return members;
    }

    public Dictionary<string, List<HashSet<string>>> BuildTestEvidence(
        ImmutableArray<AdditionalText> additionalFiles,
        AnalyzerConfigOptionsProvider optionsProvider,
        Dictionary<(string Type, string Member, int Arity), (int Hash, int Lines)> headMembers,
        TestLinkSettings settings,
        CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, List<HashSet<string>>>(StringComparer.Ordinal);

        foreach (var file in additionalFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!optionsProvider.GetOptions(file).TryGetValue(ChangedTestFileMetadata, out var marker)
                || !string.Equals(marker, "true", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = file.GetText(cancellationToken);
            if (text is null)
            {
                continue;
            }

            var root = CSharpSyntaxTree.ParseText(text, cancellationToken: cancellationToken).GetRoot(cancellationToken);
            var typeRefs = CollectTypeRefs(root);
            if (typeRefs.Count == 0)
            {
                continue; // a test file that names no type cannot be about one.
            }

            foreach (var node in root.DescendantNodes())
            {
                if (node is not MethodDeclarationSyntax method)
                {
                    continue;
                }

                if (!HasTestAttribute(method, settings.TestAttributeNames) || IsSkipped(method))
                {
                    continue;
                }

                if (!TryBuildKey(method, out var key))
                {
                    continue;
                }

                var body = GetBody(method);
                if (body is null)
                {
                    continue;
                }

                // Only tests added or changed by THIS change give credit: a whitespace edit to an old
                // test does not, because the hash ignores trivia.
                var analysis = AnalyzeBody(body, text);
                if (headMembers.TryGetValue(key, out var head) && head.Hash == analysis.Hash)
                {
                    continue;
                }

                if (!HasAssertion(body, settings.AssertionPatterns))
                {
                    continue;
                }

                var invoked = CollectInvoked(body);
                if (invoked.Count == 0)
                {
                    continue;
                }

                foreach (var type in typeRefs)
                {
                    if (!evidence.TryGetValue(type, out var sets))
                    {
                        sets = new List<HashSet<string>>();
                        evidence[type] = sets;
                    }

                    sets.Add(invoked);
                }
            }
        }

        return evidence;
    }

    public TestLinkViolation? Check(
        MemberDeclarationSyntax member,
        Dictionary<(string Type, string Member, int Arity), (int Hash, int Lines)> headMembers,
        Dictionary<string, List<HashSet<string>>> evidence,
        TestLinkSettings settings,
        CancellationToken cancellationToken)
    {
        if (!IsNonPrivate(member))
        {
            return null; // a test cannot call a private member directly; it is exercised through the surface.
        }

        if (!TryBuildKey(member, out var key))
        {
            return null;
        }

        var body = GetBody(member);
        if (body is null)
        {
            return null; // abstract, partial or interface member: nothing executes.
        }

        var text = member.SyntaxTree.GetText(cancellationToken);
        var analysis = AnalyzeBody(body, text);

        if (headMembers.TryGetValue(key, out var head))
        {
            if (head.Hash == analysis.Hash)
            {
                return null; // unchanged.
            }

            if (analysis.Lines - head.Lines < settings.MinAddedLines)
            {
                return null; // modified, but did not grow by the floor.
            }
        }
        else if (analysis.Lines < settings.MinAddedLines)
        {
            return null; // new, but too small to warrant a dedicated test.
        }

        var typeName = key.Type;
        var memberName = member is ConstructorDeclarationSyntax ? typeName : key.Member;

        var referencing = 0;
        if (evidence.TryGetValue(typeName, out var sets))
        {
            referencing = sets.Count;
            foreach (var invoked in sets)
            {
                if (invoked.Contains(memberName))
                {
                    return null; // a related test invokes this member.
                }
            }
        }

        var location = GetIdentifierLocation(member);
        return new TestLinkViolation(typeName + "." + memberName, memberName, typeName, referencing, location);
    }

    // --- syntax helpers ---

    private static SyntaxNode? GetBody(SyntaxNode node)
    {
        switch (node)
        {
            case MethodDeclarationSyntax method:
                return (SyntaxNode?)method.Body ?? method.ExpressionBody?.Expression;
            case ConstructorDeclarationSyntax ctor:
                return (SyntaxNode?)ctor.Body ?? ctor.ExpressionBody?.Expression;
            default:
                return null;
        }
    }

    private static bool TryBuildKey(SyntaxNode node, out (string Type, string Member, int Arity) key)
    {
        key = default;
        if (node.Parent is not TypeDeclarationSyntax type)
        {
            return false;
        }

        switch (node)
        {
            case MethodDeclarationSyntax method:
                key = (type.Identifier.ValueText, method.Identifier.ValueText, method.ParameterList.Parameters.Count);
                return true;
            case ConstructorDeclarationSyntax ctor:
                key = (type.Identifier.ValueText, ctor.Identifier.ValueText, ctor.ParameterList.Parameters.Count);
                return true;
            default:
                return false;
        }
    }

    private static bool IsNonPrivate(MemberDeclarationSyntax member)
    {
        foreach (var modifier in member.Modifiers)
        {
            switch (modifier.Kind())
            {
                case SyntaxKind.PublicKeyword:
                case SyntaxKind.InternalKeyword:
                case SyntaxKind.ProtectedKeyword:
                    return true;
            }
        }

        return false;
    }

    // A rolling hash over token kinds and values (identifiers included, so a rename registers as a
    // change) plus the count of distinct lines that carry a token. One pass, no substring, no
    // per-token line-span allocation: SourceText.Lines.IndexOf is a binary search.
    private static (int Hash, int Lines) AnalyzeBody(SyntaxNode body, SourceText text)
    {
        var hash = 17;
        var lines = new HashSet<int>();
        foreach (var token in body.DescendantTokens())
        {
            unchecked
            {
                hash = (hash * 31) + token.RawKind;
                hash = (hash * 31) + token.ValueText.GetHashCode();
            }

            lines.Add(text.Lines.IndexOf(token.SpanStart));
        }

        return (hash, lines.Count);
    }

    private static bool HasTestAttribute(MethodDeclarationSyntax method, ImmutableArray<string> names)
    {
        foreach (var list in method.AttributeLists)
        {
            foreach (var attribute in list.Attributes)
            {
                var name = ShortName(attribute.Name);
                foreach (var candidate in names)
                {
                    if (name.Equals(candidate, StringComparison.Ordinal)
                        || name.EndsWith(candidate, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsSkipped(MethodDeclarationSyntax method)
    {
        if (HasSkippingAttribute(method.AttributeLists))
        {
            return true;
        }

        return method.Parent is TypeDeclarationSyntax type && HasSkippingAttribute(type.AttributeLists);
    }

    private static bool HasSkippingAttribute(SyntaxList<AttributeListSyntax> lists)
    {
        foreach (var list in lists)
        {
            foreach (var attribute in list.Attributes)
            {
                var name = ShortName(attribute.Name);
                if (name.Equals("Ignore", StringComparison.Ordinal) || name.Equals("Explicit", StringComparison.Ordinal))
                {
                    return true;
                }

                if (attribute.ArgumentList is null)
                {
                    continue;
                }

                foreach (var argument in attribute.ArgumentList.Arguments)
                {
                    if (argument.NameEquals?.Name.Identifier.ValueText.Equals("Skip", StringComparison.Ordinal) == true)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool HasAssertion(SyntaxNode body, ImmutableArray<string> patterns)
    {
        foreach (var node in body.DescendantNodes())
        {
            if (node is not InvocationExpressionSyntax invocation)
            {
                continue;
            }

            if (invocation.Expression is MemberAccessExpressionSyntax access)
            {
                if (StartsWithAny(access.Name.Identifier.ValueText, patterns))
                {
                    return true;
                }

                if (access.Expression is IdentifierNameSyntax receiver && StartsWithAny(receiver.Identifier.ValueText, patterns))
                {
                    return true;
                }
            }
            else if (invocation.Expression is IdentifierNameSyntax name && StartsWithAny(name.Identifier.ValueText, patterns))
            {
                return true;
            }
        }

        return false;
    }

    // The member names a test exercises: invocation names and constructor type names, ignoring
    // nameof/typeof arguments (they mention without calling) and calls the test swallows in a
    // catch-all try (they cannot assert on an outcome).
    private static HashSet<string> CollectInvoked(SyntaxNode body)
    {
        var invoked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in body.DescendantNodes())
        {
            switch (node)
            {
                case InvocationExpressionSyntax invocation:
                {
                    var name = InvokedName(invocation);
                    if (name != null && !IsInNameofOrTypeof(invocation) && !IsInSwallowingTry(invocation))
                    {
                        invoked.Add(name);
                    }

                    break;
                }

                case ObjectCreationExpressionSyntax creation:
                {
                    var name = SimpleTypeName(creation.Type);
                    if (name != null && !IsInNameofOrTypeof(creation) && !IsInSwallowingTry(creation))
                    {
                        invoked.Add(name);
                    }

                    break;
                }
            }
        }

        return invoked;
    }

    private static string? InvokedName(InvocationExpressionSyntax invocation)
    {
        switch (invocation.Expression)
        {
            case MemberAccessExpressionSyntax access:
                return access.Name.Identifier.ValueText;
            case IdentifierNameSyntax name:
                return name.Identifier.ValueText;
            case GenericNameSyntax generic:
                return generic.Identifier.ValueText;
            default:
                return null;
        }
    }

    // Type identifiers used in a declared-type position (construction, variable/field/parameter/
    // property type, base list, cast). Deliberately NOT generic type arguments, so a test that only
    // mocks the interface (Substitute.For<IFoo>(), new Mock<Foo>()) does not count as referencing the
    // concrete type it never constructs.
    private static HashSet<string> CollectTypeRefs(SyntaxNode root)
    {
        var refs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                case ObjectCreationExpressionSyntax creation:
                    Add(refs, creation.Type);
                    break;
                case VariableDeclarationSyntax variable:
                    Add(refs, variable.Type);
                    break;
                case ParameterSyntax parameter:
                    Add(refs, parameter.Type);
                    break;
                case PropertyDeclarationSyntax property:
                    Add(refs, property.Type);
                    break;
                case CastExpressionSyntax cast:
                    Add(refs, cast.Type);
                    break;
                case SimpleBaseTypeSyntax baseType:
                    Add(refs, baseType.Type);
                    break;
            }
        }

        return refs;
    }

    private static void Add(HashSet<string> set, TypeSyntax? type)
    {
        var name = SimpleTypeName(type);
        if (name != null)
        {
            set.Add(name);
        }
    }

    private static string? SimpleTypeName(TypeSyntax? type)
    {
        switch (type)
        {
            case IdentifierNameSyntax identifier:
                return identifier.Identifier.ValueText;
            case GenericNameSyntax generic:
                return generic.Identifier.ValueText;
            case QualifiedNameSyntax qualified:
                return qualified.Right.Identifier.ValueText;
            case NullableTypeSyntax nullable:
                return SimpleTypeName(nullable.ElementType);
            default:
                return null;
        }
    }

    private static bool IsInNameofOrTypeof(SyntaxNode node)
    {
        for (var current = node.Parent; current != null; current = current.Parent)
        {
            if (current is TypeOfExpressionSyntax)
            {
                return true;
            }

            if (current is InvocationExpressionSyntax invocation
                && invocation.Expression is IdentifierNameSyntax name
                && name.Identifier.ValueText.Equals("nameof", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInSwallowingTry(SyntaxNode node)
    {
        for (var current = node.Parent; current != null; current = current.Parent)
        {
            if (current is TryStatementSyntax tryStatement
                && tryStatement.Block.Span.Contains(node.Span)
                && HasSwallowingCatch(tryStatement))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSwallowingCatch(TryStatementSyntax tryStatement)
    {
        foreach (var clause in tryStatement.Catches)
        {
            var catchAll = clause.Declaration is null
                || SimpleTypeName(clause.Declaration.Type)?.Equals("Exception", StringComparison.Ordinal) == true;
            if (!catchAll)
            {
                continue;
            }

            var rethrows = false;
            foreach (var inner in clause.Block.DescendantNodes())
            {
                if (inner is ThrowStatementSyntax)
                {
                    rethrows = true;
                    break;
                }
            }

            if (!rethrows)
            {
                return true;
            }
        }

        return false;
    }

    private static string ShortName(NameSyntax name)
    {
        switch (name)
        {
            case IdentifierNameSyntax identifier:
                return identifier.Identifier.ValueText;
            case QualifiedNameSyntax qualified:
                return qualified.Right.Identifier.ValueText;
            case GenericNameSyntax generic:
                return generic.Identifier.ValueText;
            default:
                return name.ToString();
        }
    }

    private static bool StartsWithAny(string value, ImmutableArray<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (value.StartsWith(pattern, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static Location GetIdentifierLocation(MemberDeclarationSyntax member)
    {
        switch (member)
        {
            case MethodDeclarationSyntax method:
                return method.Identifier.GetLocation();
            case ConstructorDeclarationSyntax ctor:
                return ctor.Identifier.GetLocation();
            default:
                return member.GetLocation();
        }
    }

    // --- config helpers ---

    private static int GetInt(AnalyzerConfigOptions options, string key, int fallback)
    {
        if (options.TryGetValue(key, out var value)
            && !string.IsNullOrWhiteSpace(value)
            && int.TryParse(value.Trim(), out var parsed)
            && parsed >= 0)
        {
            return parsed;
        }

        return fallback;
    }

    private static ImmutableArray<string> GetList(AnalyzerConfigOptions options, string key, string fallback)
    {
        var raw = options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var entry in raw.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = entry.Trim();
            if (trimmed.Length != 0)
            {
                builder.Add(trimmed);
            }
        }

        return builder.ToImmutable();
    }
}
