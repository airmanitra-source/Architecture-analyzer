namespace HR.Module.Models.Data.Providers
{
    public interface IHrProvider
    {
        Task<EmployeeDataModel?> GetEmployeeByIdAsync(int id, CancellationToken cancellationToken);

        Task<IReadOnlyCollection<EmployeeDataModel>> GetEmployeesAsync(CancellationToken cancellationToken);

        Task<EmployeeDataModel> CreateEmployeeAsync(EmployeeDataModel employee, CancellationToken cancellationToken);

        Task<EmployeeDataModel?> UpdateEmployeeAsync(EmployeeDataModel employee, CancellationToken cancellationToken);

        Task<bool> DeleteEmployeeAsync(int id, CancellationToken cancellationToken);
    }
}