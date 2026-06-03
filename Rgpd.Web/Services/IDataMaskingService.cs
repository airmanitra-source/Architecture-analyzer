using System.Security.Claims;

namespace Rgpd.Web.Services;

public interface IDataMaskingService
{
    string MaskValue(object? value, string purpose, ClaimsPrincipal principal);
}
