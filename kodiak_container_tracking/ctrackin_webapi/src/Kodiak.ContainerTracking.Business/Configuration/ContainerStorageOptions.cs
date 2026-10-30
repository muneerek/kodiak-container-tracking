namespace Kodiak.ContainerTracking.Business.Configuration;
public sealed class ContainerStorageOptions
{
    public string RootPath { get; set; } = "App_Data/ContainerAttachments";
    public int MaxFileBytes { get; set; } = 10485760;
    public int MaxFilesPerContainer { get; set; } = 20;
}
