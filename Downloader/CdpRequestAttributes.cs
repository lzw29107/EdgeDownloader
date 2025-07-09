namespace EdgeDownloader.Downloader;

public class CdpRequestAttributes(EdgeProductType productType) // Windows.Services.DeliveryCatalog.SimpleFileSolution.Model.CdpRequestAttributes
{
    public string Product { get; set; } = productType.ToString();
    public TargetingAttributes TargetingAttributes { get; set; } = new TargetingAttributes();
}
