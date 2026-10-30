using System.Text.RegularExpressions;

namespace Kodiak.ContainerTracking.Business.Configuration;

public sealed class UniqueCodeOptions
{
    public const string SectionName = "UniqueCodes";
    public string EmployeePrefix { get; set; } = "EMP-";
    public string ConsigneePrefix { get; set; } = "CON-";
    public string ShipperPrefix { get; set; } = "SHP-";
    public int Padding { get; set; } = 6;

    public bool IsValid()
    {
        var prefixes = new[] { EmployeePrefix, ConsigneePrefix, ShipperPrefix };
        return Padding is >= 1 and <= 18
            && prefixes.All(x => !string.IsNullOrWhiteSpace(x) && Regex.IsMatch(x, "^[A-Z][A-Z0-9_-]{0,19}$"))
            && prefixes.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 3;
    }
}
