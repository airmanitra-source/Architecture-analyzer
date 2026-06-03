namespace Rgpd.Web.Contracts;

public interface IConsentRepository
{
    Task<IReadOnlyCollection<string>> GetActivePurposesAsync(string userId, CancellationToken cancellationToken);
    Task UpsertConsentAsync(string userId, string purpose, bool accepted, CancellationToken cancellationToken);
}
