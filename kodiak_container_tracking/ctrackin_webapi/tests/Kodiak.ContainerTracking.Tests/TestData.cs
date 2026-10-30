using Kodiak.ContainerTracking.Business.Contracts;
namespace Kodiak.ContainerTracking.Tests;
internal static class TestData
{
    internal const string Password = "Test-only-Password42!";
    internal const string SigningKey = "test-only-signing-key-for-kodiak-at-least-32-bytes-long";
    internal static EmployeeRequest Employee(string firstName = "Aisha") => new(firstName, "Khan", new DateOnly(1990, 1, 1), "aisha@example.com");
    internal static CompanyRequest Consignee(string name = "Customer") => new(name, "Contact Name", "contact@example.com", "+919876543210", "24 Port Road");
    internal static ShipperRequest Shipper(int consigneeId, string name = "Exporter") => new(name, "Contact Person", "shipper@example.com", "+919876543210", "36 Dock Road", consigneeId);
}
