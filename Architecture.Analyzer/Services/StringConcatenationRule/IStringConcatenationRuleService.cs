using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer.Services.StringConcatenationRule;

internal interface IStringConcatenationRuleService
{
    // True when the operation is a '+' / '+=' whose result is a runtime string concatenation
    // (the built-in string operator, non-constant). User-defined '+' operators and compile-time
    // constant concatenations (folded literals, including const fields) are intentionally allowed.
    bool IsForbiddenConcatenation(IOperation? operation);
}
