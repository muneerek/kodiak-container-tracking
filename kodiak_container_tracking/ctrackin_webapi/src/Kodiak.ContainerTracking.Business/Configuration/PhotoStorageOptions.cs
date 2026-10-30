namespace Kodiak.ContainerTracking.Business.Configuration;
public sealed class PhotoStorageOptions
{
    public string RootPath { get; set; } = "App_Data/Photos";
    public int MaxFileBytes { get; set; } = 2097152;
}
