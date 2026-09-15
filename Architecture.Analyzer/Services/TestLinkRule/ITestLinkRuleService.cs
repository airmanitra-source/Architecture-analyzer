using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.TestLinkRule;

internal interface ITestLinkRuleService
{
    TestLinkSettings GetSettings(AnalyzerConfigOptions options);

    // Absolute paths of every file this change touched, from the shipped MSBuild target. Empty when
    // the target did not run: the rule then stays silent.
    HashSet<string> ReadChangedFiles(
        ImmutableArray<AdditionalText> additionalFiles,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken);

    // Paths of the changed test files the target handed over as source, so a test project's own
    // compilation never mistakes its test methods for production members.
    HashSet<string> ReadTestFilePaths(
        ImmutableArray<AdditionalText> additionalFiles,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken);

    // Every method and constructor as it stands at HEAD, keyed by type + name + arity, with a hash of
    // its body and its line count. A member absent here is new; a member whose hash differs was
    // modified. Built from the HEAD copies the target already ships (ArchitectureBaselineFor).
    Dictionary<(string Type, string Member, int Arity), (int Hash, int Lines)> BuildHeadMembers(
        ImmutableArray<AdditionalText> additionalFiles,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken);

    // For every type a changed test file references, the set of members each qualifying test method in
    // that file invokes. A test method qualifies when it is new or modified, carries a test attribute,
    // is not skipped and contains an assertion. Keyed by type name so the production side finds its
    // evidence in O(1).
    Dictionary<string, List<HashSet<string>>> BuildTestEvidence(
        ImmutableArray<AdditionalText> additionalFiles,
        AnalyzerConfigOptionsProvider optionsProvider,
        Dictionary<(string Type, string Member, int Arity), (int Hash, int Lines)> headMembers,
        TestLinkSettings settings,
        CancellationToken cancellationToken);

    // Null when the member is not accountable (unchanged, private, below the floor) or is already
    // exercised by a related test; a violation otherwise.
    TestLinkViolation? Check(
        MemberDeclarationSyntax member,
        Dictionary<(string Type, string Member, int Arity), (int Hash, int Lines)> headMembers,
        Dictionary<string, List<HashSet<string>>> evidence,
        TestLinkSettings settings,
        CancellationToken cancellationToken);
}
