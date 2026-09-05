namespace Architecture.Analyzer.Models;

// A single "ClassPattern>ForbiddenTypePattern" rule: methods of a type whose name
// matches ClassPattern must not declare a parameter whose type name matches
// ForbiddenTypePattern. Each side is a NamePattern, so it can target a suffix
// ("*Module"), a prefix ("Module*"), the whole name ("Module") or a substring
// ("*Module*").
internal sealed class MethodArgumentTypeBarrierRule
{
    public MethodArgumentTypeBarrierRule(NamePattern classPattern, NamePattern forbiddenTypePattern)
    {
        ClassPattern = classPattern;
        ForbiddenTypePattern = forbiddenTypePattern;
    }

    public NamePattern ClassPattern { get; }

    public NamePattern ForbiddenTypePattern { get; }
}
