using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Kodiak.ContainerTracking.Business.Configuration;
using Kodiak.ContainerTracking.Business.Exceptions;
using Kodiak.ContainerTracking.Business.Services;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.Extensions.Options;
namespace Kodiak.ContainerTracking.Api.Infrastructure;
public sealed partial class ContainerFileStorage(IWebHostEnvironment environment, IOptions<ContainerStorageOptions> options) : IContainerFileStorage
{
    private string Root => Path.GetFullPath(options.Value.RootPath, environment.ContentRootPath);
    public async Task<PreparedContainerFile> SaveAsync(Stream input, string fileName, ContainerFileKind kind, CancellationToken ct)
    {
        if (!Enum.IsDefined(kind)) throw Invalid();
        var name = Path.GetFileName(fileName.Replace('\\', '/')); name = string.Concat(name.Where(c => !char.IsControl(c)));
        if (name.Length is < 1 or > 200) throw new BusinessException(400, "invalid_file_name", "Use a file name of 1 to 200 characters.");
        var ext = Path.GetExtension(name).ToLowerInvariant();
        using var buffer = new MemoryStream(); var chunk = new byte[81920]; int length;
        while ((length = await input.ReadAsync(chunk, ct)) > 0) { if (buffer.Length + length > options.Value.MaxFileBytes) throw new BusinessException(400, "file_size", "File exceeds the configured upload limit."); await buffer.WriteAsync(chunk.AsMemory(0, length), ct); }
        var bytes = buffer.ToArray(); if (bytes.Length == 0) throw Invalid(); string contentType;
        if (ext is ".jpg" or ".jpeg") { if (bytes.Length < 4 || bytes[0] != 255 || bytes[1] != 216 || bytes[2] != 255 || bytes[^2] != 255 || bytes[^1] != 217) throw Invalid(); ext = ".jpg"; contentType = "image/jpeg"; }
        else if (ext == ".png") { if (bytes.Length < 24 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})) throw Invalid(); contentType = "image/png"; }
        else if (ext == ".webp") { if (bytes.Length < 12 || !bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8)) throw Invalid(); contentType = "image/webp"; }
        else
        {
            if (kind == ContainerFileKind.Photo) throw Invalid();
            if (ext == ".pdf") { if (!bytes.AsSpan().StartsWith("%PDF-"u8)) throw Invalid(); contentType = "application/pdf"; }
            else if (ext == ".txt") { try { _ = new UTF8Encoding(false, true).GetString(bytes); } catch (DecoderFallbackException) { throw Invalid(); } if (bytes.Contains((byte)0)) throw Invalid(); contentType = "text/plain"; }
            else if (ext == ".xls")
            {
                if (bytes.Length < 512 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] {208,207,17,224,161,177,26,225})) throw Invalid();
                var directory = Encoding.Unicode.GetString(bytes);
                if ((!directory.Contains("Workbook", StringComparison.Ordinal) && !directory.Contains("Book", StringComparison.Ordinal))
                    || directory.Contains("_VBA_PROJECT", StringComparison.OrdinalIgnoreCase)) throw Invalid();
                contentType = "application/vnd.ms-excel";
            }
            else if (ext is ".docx" or ".xlsx")
            {
                try { using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read); if (zip.Entries.Count > 10000 || zip.Entries.Sum(e => e.Length) > 52428800 || zip.GetEntry("[Content_Types].xml") == null || zip.GetEntry(ext == ".docx" ? "word/document.xml" : "xl/workbook.xml") == null || zip.Entries.Any(e => e.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase))) throw Invalid(); }
                catch (InvalidDataException) { throw Invalid(); }
                contentType = ext == ".docx" ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            }
            else throw Invalid();
        }
        var relative = $"containers/{Guid.NewGuid():N}{ext}"; var path = Resolve(relative)!;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try { await File.WriteAllBytesAsync(path, bytes, ct); } catch { if (File.Exists(path)) File.Delete(path); throw; }
        return new(relative, name, contentType, bytes.LongLength);
    }
    public Task<ContainerDownload?> OpenAsync(string path, string contentType, CancellationToken ct)
    { var fullPath = Resolve(path); return Task.FromResult<ContainerDownload?>(fullPath != null && File.Exists(fullPath) ? new(File.OpenRead(fullPath), contentType) : null); }
    public Task DeleteAsync(string path, CancellationToken ct) { var fullPath = Resolve(path); if (fullPath != null && File.Exists(fullPath)) File.Delete(fullPath); return Task.CompletedTask; }
    private string? Resolve(string path) => StoragePath().IsMatch(path) ? Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar)) : null;
    [GeneratedRegex(@"^containers/[a-f0-9]{32}\.(jpg|png|webp|pdf|docx|xls|xlsx|txt)$", RegexOptions.CultureInvariant)] private static partial Regex StoragePath();
    private static BusinessException Invalid() => new(400, "invalid_attachment", "Photos must be JPEG/PNG/WebP. Documents must be PDF, DOCX, XLS/XLSX or UTF-8 TXT with matching file content.");
}
