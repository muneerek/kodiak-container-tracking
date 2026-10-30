namespace Kodiak.ContainerTracking.Business.Services;
public sealed record StoredPhoto(Stream Content, string ContentType);
public interface IEmployeePhotoStorage
{
    Task<string> SaveAsync(Stream content, CancellationToken ct, string folder = "employees");
    Task<StoredPhoto?> OpenAsync(string relativePath, CancellationToken ct);
    Task DeleteAsync(string relativePath, CancellationToken ct);
}
