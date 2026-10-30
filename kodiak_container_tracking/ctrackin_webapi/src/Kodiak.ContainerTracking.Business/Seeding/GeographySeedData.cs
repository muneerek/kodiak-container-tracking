using System.Globalization;
using System.Text;
using System.Text.Json;
namespace Kodiak.ContainerTracking.Business.Seeding;

internal sealed record CountrySeed(string Code, string Alpha3, string Name);
internal sealed record StateSeed(string Code, string CountryCode, string Name, string Type);
internal static class GeographySeedData
{
    internal static readonly CountrySeed[] Countries = Read<CountrySeed>("countries.json");
    internal static readonly StateSeed[] States = Read<StateSeed>("states-provinces.json");
    private static T[] Read<T>(string name)
    {
        using var stream = typeof(GeographySeedData).Assembly.GetManifestResourceStream($"Kodiak.ContainerTracking.Business.Seeding.Data.{name}")
            ?? throw new InvalidOperationException($"Missing geography seed resource: {name}");
        return JsonSerializer.Deserialize<T[]>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException($"Invalid geography seed resource: {name}");
    }
    internal static string Normalize(string value) => string.Concat(value.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))).ToUpperInvariant();
}
