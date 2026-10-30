using System.ComponentModel.DataAnnotations;
using Kodiak.ContainerTracking.Business.Exceptions;

namespace Kodiak.ContainerTracking.Business.Services;

internal static class ServiceValidation
{
    internal static void Validate(object request)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true))
            throw new BusinessException(400, "validation_failed", string.Join(" ", errors.Select(x => x.ErrorMessage)));
    }

    internal static string Required(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new BusinessException(400, "validation_failed", $"{field} is required.");
        return value.Trim();
    }

    internal static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    internal static void Version(long expected, long actual)
    {
        if (expected != actual) throw new BusinessException(412, "stale_version", "The record changed. Reload it before saving.");
    }

    internal static void Page(int page, int pageSize)
    {
        if (page is < 1 or > 1000000 || pageSize is < 1 or > 100)
            throw new BusinessException(400, "invalid_pagination", "Page must be 1–1000000 and pageSize must be 1–100.");
    }

    internal static void Dob(DateOnly? dob)
    {
        if (dob > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new BusinessException(400, "invalid_dob", "Date of birth cannot be in the future.");
    }
}
