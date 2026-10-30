namespace Kodiak.ContainerTracking.DataAccess.Entities;

public sealed class OrganizationHierarchy : MasterRecord
{
    public int CompanyId { get; set; }
    public int? ParentOrgId { get; set; }
    public Company Company { get; set; } = null!;
    public OrganizationHierarchy? Parent { get; set; }
}
