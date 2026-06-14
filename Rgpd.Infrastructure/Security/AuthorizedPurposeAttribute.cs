using System;

namespace Rgpd.Infrastructure.Security;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AuthorizedPurposeAttribute : Attribute
{
    public AuthorizedPurposeAttribute(Purpose purpose, LegalBasis legalBasis, AccessRole accessRole)
    {
        Purpose = purpose;
        LegalBasis = legalBasis;
        AccessRole = accessRole;
    }

    public Purpose Purpose { get; }

    public LegalBasis LegalBasis { get; }

    public AccessRole AccessRole { get; }
}
