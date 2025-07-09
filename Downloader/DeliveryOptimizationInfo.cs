namespace EdgeDownloader.Downloader;

public class DeliveryOptimizationInfo
{
    public string CatalogId { get; set; } = string.Empty;
    public DeliveryOptimizationProperties Properties { get; set; } = new();
}
