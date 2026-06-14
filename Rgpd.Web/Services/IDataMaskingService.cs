using System.Security.Claims;
using Rgpd.Infrastructure.Security;

namespace Rgpd.Web.Services;

public interface IDataMaskingService
{
    string MaskValue(object? value, PersonalDataAccessContext accessContext, ClaimsPrincipal principal);
}
