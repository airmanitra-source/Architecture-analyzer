using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer.Models;

internal sealed class TestLinkViolation
{
    public TestLinkViolation(string displayName, string memberName, string typeName, int referencingTestCount, Location location)
    {
        DisplayName = displayName;
        MemberName = memberName;
        TypeName = typeName;
        ReferencingTestCount = referencingTestCount;
        Location = location;
    }

    // "Type.Member" for the message.
    public string DisplayName { get; }

    public string MemberName { get; }

    public string TypeName { get; }

    // How many changed test methods reference the type but do not call this member. 0 means no test
    // touches the type at all; a positive number means tests exist but none exercises this member.
    public int ReferencingTestCount { get; }

    public Location Location { get; }
}
