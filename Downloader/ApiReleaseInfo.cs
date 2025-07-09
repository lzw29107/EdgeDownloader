namespace EdgeDownloader.Downloader;

public class ApiReleaseInfo
{
    public string Architecture { get; set; }
    public List<ApiArtifactInfo> Artifacts { get; set; }
    public DateTime ExpectedExpiryDate { get; set; }
    public List<string> CVEs { get; set; }
    public string Platform { get; set; }
    public Version ProductVersion { get; set; }
    public DateTime PublishedTime { get; set; }
    public int ReleaseId { get; set; }

    public ApiReleaseInfo()
    {
        Architecture = string.Empty;
        Artifacts = [];
        ExpectedExpiryDate = DateTime.MinValue;
        CVEs = [];
        Platform = string.Empty;
        ProductVersion = Constants.InvalidVersion;
        PublishedTime = DateTime.MinValue;
        ReleaseId = 0;
    }
}
