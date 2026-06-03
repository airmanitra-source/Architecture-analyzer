using System;

namespace Rgpd.Infrastructure.Security;

[AttributeUsage(AttributeTargets.Property)]
public sealed class PersonalDataAttribute : Attribute
{
    public required string Purpose { get; init; }
}
