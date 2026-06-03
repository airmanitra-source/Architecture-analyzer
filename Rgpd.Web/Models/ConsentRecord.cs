namespace Rgpd.Web.Models;

public sealed class ConsentRecord
{
    public string UserId { get; init; } = string.Empty;
    public string Purpose { get; init; } = string.Empty;
    public DateTimeOffset? ConsentedAt { get; init; }
    public DateTimeOffset? WithdrawnAt { get; init; }
}
