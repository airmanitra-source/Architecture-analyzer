using Rgpd.SqlSetup.ViewModels;

namespace Rgpd.SqlSetup.Contracts;

public interface ISqlSetupService
{
    Task<IReadOnlyList<SqlFieldViewModel>> GetFieldsAsync(CancellationToken cancellationToken);
    Task ApplyMaskingAsync(AddFieldRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<RlsPolicyViewModel>> GetPoliciesAsync(CancellationToken cancellationToken);
    Task SyncRlsPoliciesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<RoleMappingViewModel>> GetRoleMappingsAsync(CancellationToken cancellationToken);
    Task UpsertRoleMappingAsync(RoleMappingViewModel request, CancellationToken cancellationToken);
}
