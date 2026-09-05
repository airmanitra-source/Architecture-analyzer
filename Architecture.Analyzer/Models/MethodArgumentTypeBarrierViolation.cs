using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer.Models;

internal sealed class MethodArgumentTypeBarrierViolation
{
    public MethodArgumentTypeBarrierViolation(
        string methodName,
        string className,
        string parameterTypeName,
        string classPattern,
        string forbiddenTypePattern,
        Location location)
    {
        MethodName = methodName;
        ClassName = className;
        ParameterTypeName = parameterTypeName;
        ClassPattern = classPattern;
        ForbiddenTypePattern = forbiddenTypePattern;
        Location = location;
    }

    public string MethodName { get; }

    public string ClassName { get; }

    public string ParameterTypeName { get; }

    public string ClassPattern { get; }

    public string ForbiddenTypePattern { get; }

    public Location Location { get; }
}
