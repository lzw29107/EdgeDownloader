namespace EdgeDownloader.Downloader;

public class ApiDownloadInfo
{
    public string Url { get; set; } = string.Empty;
    public string FileId { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public HashesInfo Hashes { get; set; } = new();
    public DeliveryOptimizationInfo DeliveryOptimization { get; set; } = new();
}
