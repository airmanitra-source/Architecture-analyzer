using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer.Services.StringConcatenationRule;

internal interface IStringConcatenationRuleService
{
    // True when the operation is a '+' / '+=' whose result is a runtime string concatenation
    // (the built-in string operator, non-constant). User-defined '+' operators and compile-time
    // constant concatenations (folded literals, including const fields) are intentionally allowed.
    bool IsForbiddenConcatenation(IOperation? operation);

    // True when the operation is an interpolated string ($"...") that actually interpolates a
    // value at runtime. A hole-free $"text" and a compile-time constant interpolated string
    // (all-constant holes, including const fields) are intentionally allowed.
    bool IsForbiddenInterpolation(IOperation? operation);
}
