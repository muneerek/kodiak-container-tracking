using System.Text.RegularExpressions;
using Kodiak.ContainerTracking.Business.Configuration;
using Kodiak.ContainerTracking.Business.Exceptions;
using Kodiak.ContainerTracking.Business.Services;
using Microsoft.Extensions.Options;
namespace Kodiak.ContainerTracking.Api.Infrastructure;

public sealed partial class EmployeePhotoStorage(IWebHostEnvironment environment, IOptions<PhotoStorageOptions> options) : IEmployeePhotoStorage
{
    private string Root => Path.GetFullPath(options.Value.RootPath, environment.ContentRootPath);
    public async Task<string> SaveAsync(Stream content, CancellationToken ct, string folder = "employees")
    {
        if (folder is not ("employees" or "shippers")) throw new ArgumentOutOfRangeException(nameof(folder));
        using var buffer = new MemoryStream();
        var chunk = new byte[81920]; int count;
        while ((count = await content.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + count > options.Value.MaxFileBytes) throw new BusinessException(400, "photo_size", "Photo exceeds the configured upload limit.");
            await buffer.WriteAsync(chunk.AsMemory(0, count), ct);
        }
        var bytes = buffer.ToArray();
        var png = bytes.Length > 24 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var jpeg = bytes.Length > 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 && bytes[^2] == 255 && bytes[^1] == 217;
        if (!png && !jpeg) throw new BusinessException(400, "photo_format", "Choose a JPEG or PNG photo.");
        var relativePath = $"{folder}/{Guid.NewGuid():N}.{(png ? "png" : "jpg")}";
        var path = Resolve(relativePath)!;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try { await File.WriteAllBytesAsync(path, bytes, ct); }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
        return relativePath;
    }
    public Task<StoredPhoto?> OpenAsync(string relativePath, CancellationToken ct)
    {
        var path = Resolve(relativePath);
        if (path == null || !File.Exists(path)) return Task.FromResult<StoredPhoto?>(null);
        return Task.FromResult<StoredPhoto?>(new(File.OpenRead(path), relativePath.EndsWith(".png", StringComparison.Ordinal) ? "image/png" : "image/jpeg"));
    }
    public Task DeleteAsync(string relativePath, CancellationToken ct)
    {
        var path = Resolve(relativePath); if (path != null && File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
    private string? Resolve(string relativePath)
    {
        if (!PhotoPath().IsMatch(relativePath)) return null;
        return Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
    [GeneratedRegex(@"^(employees|shippers)/[a-f0-9]{32}\.(jpg|png)$", RegexOptions.CultureInvariant)]
    private static partial Regex PhotoPath();
}
