using Kodiak.ContainerTracking.DataAccess.Repositories;
namespace Kodiak.ContainerTracking.Business.Contracts;
public sealed record OverviewProfile(int UserType, string CompanyName, string Code, bool CanRegister, string? ConsigneeName = null);
public sealed record OverviewResponse(OverviewCounts Counts, IReadOnlyList<OverviewStage> Stages, IReadOnlyList<OverviewShipment> Recent,
    IReadOnlyList<OverviewShipment> Attention, IReadOnlyList<OverviewRoute> Routes, IReadOnlyList<OverviewActivity> Activity, DateOnly BusinessDate, DateTimeOffset GeneratedAt, OverviewProfile Profile, IReadOnlyList<OverviewPartner> Partners, int PartnerCount);
