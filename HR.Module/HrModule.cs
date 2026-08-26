using HR.Module.Models.Business;
using HR.Module.Models.Data.Providers;

namespace HR.Module
{
    public sealed class HrModule : IHrModule
    {
        private readonly IHrProvider _provider;

        public HrModule(IHrProvider provider)
        {
            _provider = provider;
        }

        public Task<EmployeeBusinessModel?> GetEmployeeByIdAsync(int id, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<EmployeeBusinessModel>> GetEmployeesAsync(CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<EmployeeBusinessModel> CreateEmployeeAsync(EmployeeBusinessModel employee, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<EmployeeBusinessModel?> UpdateEmployeeAsync(EmployeeBusinessModel employee, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            var x = new EmployeeBusinessModel();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
            throw new NotImplementedException();
        }

        public Task<bool> DeleteEmployeeAsync(int id, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }
}
