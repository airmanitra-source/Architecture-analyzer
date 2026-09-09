using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Architecture.Analyzer.Services.StringConcatenationRule;

internal sealed class StringConcatenationRuleService : IStringConcatenationRuleService
{
    public bool IsForbiddenConcatenation(IOperation? operation)
    {
        switch (operation)
        {
            // a + b  (and "x" + value, value + 1, ...) — flagged when it is the built-in string
            // '+' and not a compile-time constant.
            case IBinaryOperation binary:
                return binary.OperatorKind == BinaryOperatorKind.Add
                       && binary.OperatorMethod is null
                       && !binary.ConstantValue.HasValue
                       && IsString(binary.Type);

            // s += "x" — a compound assignment is always a runtime concatenation (never constant).
            case ICompoundAssignmentOperation compound:
                return compound.OperatorKind == BinaryOperatorKind.Add
                       && compound.OperatorMethod is null
                       && IsString(compound.Type);

            default:
                return false;
        }
    }

    private static bool IsString(ITypeSymbol? type) => type is { SpecialType: SpecialType.System_String };
}
