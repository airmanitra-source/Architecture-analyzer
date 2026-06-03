namespace Rgpd.Infrastructure.Security;

public interface IRgpdRoleAccessor
{
    string GetCurrentSqlRole();
}
