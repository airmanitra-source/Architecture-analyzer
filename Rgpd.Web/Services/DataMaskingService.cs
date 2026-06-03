using System.Security.Claims;

namespace Rgpd.Web.Services;

public sealed class DataMaskingService : IDataMaskingService
{
    public string MaskValue(object? value, string purpose, ClaimsPrincipal principal)
    {
        var content = value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        if (principal.HasClaim($"rgpd:purpose:{purpose.ToLowerInvariant()}", "true") || principal.IsInRole("DataOfficer"))
        {
            return content;
        }

        if (content.Contains('@'))
        {
            var at = content.IndexOf('@');
            if (at <= 1)
            {
                return "***";
            }

            return $"{content[0]}***{content[at..]}";
        }

        if (content.Length <= 4)
        {
            return "****";
        }

        return $"{new string('*', Math.Max(0, content.Length - 4))}{content[^4..]}";
    }
}
