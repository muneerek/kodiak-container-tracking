namespace Kodiak.ContainerTracking.DataAccess.Entities;
public sealed class TimeZoneMaster
{
    public string Name { get; set; } = "";
    public string Abbreviation { get; set; } = "";
    public int UtcOffsetSeconds { get; set; }
    public bool IsDaylightSaving { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
}
