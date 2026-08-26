namespace HR.Module.Models.Data
{
    public sealed class EmployeeDataModel
    {
        public int Id { get; set; }

        public string Department { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string EmployeeNumber { get; set; } = string.Empty;

        public string FirstName { get; set; } = string.Empty;

        public DateTime HireDate { get; set; }

        public bool IsActive { get; set; }

        public string JobTitle { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;
    }
}
