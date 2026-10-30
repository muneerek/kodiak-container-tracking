namespace Kodiak.ContainerTracking.Business.Contracts;
public sealed record TimeZoneResponse(string Name, string Abbreviation, int UtcOffsetSeconds, bool IsDaylightSaving, DateTimeOffset ObservedAt);
