using System.Collections.Immutable;

namespace Architecture.Analyzer.Models;

internal sealed class TestLinkSettings
{
    public TestLinkSettings(
        int minAddedLines,
        ImmutableArray<string> excludePaths,
        ImmutableArray<string> testAttributeNames,
        ImmutableArray<string> assertionPatterns)
    {
        MinAddedLines = minAddedLines;
        ExcludePaths = excludePaths;
        TestAttributeNames = testAttributeNames;
        AssertionPatterns = assertionPatterns;
    }

    // Enables the rule and sets the per-member floor: a member counts as accountable only when it is
    // new with at least this many body lines, or grew by at least this many. 0 (the default) is off.
    public int MinAddedLines { get; }

    // Path fragments never held accountable (migrations, controllers, Program.cs...).
    public ImmutableArray<string> ExcludePaths { get; }

    // Attribute short names that mark a test method (Fact, Theory, Test...). Matched by name only, so
    // the analyzer never references a test framework.
    public ImmutableArray<string> TestAttributeNames { get; }

    // A test method must contain an invocation whose name or receiver starts with one of these
    // (Assert, Should, Verify...), otherwise it exercises nothing.
    public ImmutableArray<string> AssertionPatterns { get; }

    public bool IsEnabled => MinAddedLines > 0;
}
