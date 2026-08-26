using HR.Module.Models.Business;

namespace HR.Module.Models.Data.Providers
{
    public interface IHrProvider
    {
        Task<EmployeeDataModel?> GetEmployeeByIdAsync(int id, CancellationToken cancellationToken);

        Task<IReadOnlyCollection<EmployeeDataModel>> GetEmployeesAsync(CancellationToken cancellationToken);
    }
}