using HR.Module.Models.Business;

namespace HR.Module
{
    public interface IHrModule
    {
        Task<EmployeeBusinessModel?> GetEmployeeByIdAsync(int id, CancellationToken cancellationToken);

        Task<IReadOnlyCollection<EmployeeBusinessModel>> GetEmployeesAsync(CancellationToken cancellationToken);

        Task<EmployeeBusinessModel> CreateEmployeeAsync(EmployeeBusinessModel employee, CancellationToken cancellationToken);

        Task<EmployeeBusinessModel?> UpdateEmployeeAsync(EmployeeBusinessModel employee, CancellationToken cancellationToken);

        Task<bool> DeleteEmployeeAsync(int id, CancellationToken cancellationToken);
    }
}
