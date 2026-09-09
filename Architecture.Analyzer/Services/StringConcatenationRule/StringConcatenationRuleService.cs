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

    public bool IsForbiddenInterpolation(IOperation? operation)
    {
        if (operation is not IInterpolatedStringOperation interpolated)
        {
            return false;
        }

        // A compile-time constant interpolated string (all-constant holes, or none) is folded and
        // costs nothing at runtime — and a const cannot use a StringBuilder — so it is allowed.
        if (interpolated.ConstantValue.HasValue)
        {
            return false;
        }

        // Allowed when there is no real '{expr}' hole (a hole-free $"text" is just a literal).
        foreach (var part in interpolated.Parts)
        {
            if (part is IInterpolationOperation)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsString(ITypeSymbol? type) => type is { SpecialType: SpecialType.System_String };
}
