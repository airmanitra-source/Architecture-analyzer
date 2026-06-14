namespace Rgpd.Infrastructure.Security;

public sealed class PersonalDataAccessContext
{
    public required Purpose Purpose { get; init; }

    public required LegalBasis LegalBasis { get; init; }

    public required AccessRole AccessRole { get; init; }
}