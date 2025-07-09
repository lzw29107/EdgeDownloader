namespace EdgeDownloader.Downloader;

public class ContentIdType
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public Version Version { get; set; } = Constants.InvalidVersion;
}
