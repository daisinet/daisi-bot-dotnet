namespace DaisiBot.Core.Models;

public class ModelDownloadInfo
{
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsMultiModal { get; set; }
}
