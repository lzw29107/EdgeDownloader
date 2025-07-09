namespace EdgeDownloader.Downloader;

public class ApiProductInfo
{
    public string Product { get; set; }
    public List<ApiReleaseInfo> Releases { get; set; }

    public ApiProductInfo()
    {
        Product = string.Empty;
        Releases = [];
    }
}
