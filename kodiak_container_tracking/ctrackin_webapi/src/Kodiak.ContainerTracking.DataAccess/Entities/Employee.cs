namespace Kodiak.ContainerTracking.DataAccess.Entities;

public sealed class Employee : PartyRecord
{
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public DateOnly? Dob { get; set; }
    public EmployeeStatus UserStatus { get; set; } = EmployeeStatus.Active;
    public int? DesignationId { get; set; }
    public int? OrgId { get; set; }
    public Designation? Designation { get; set; }
    public OrganizationHierarchy? Organization { get; set; }
}
