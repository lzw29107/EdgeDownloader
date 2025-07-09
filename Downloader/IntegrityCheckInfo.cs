namespace EdgeDownloader.Downloader;

public class IntegrityCheckInfo
{
    public string PiecesHashFileUrl { get; set; } = string.Empty;
    public string HashOfHashes { get; set; } = string.Empty;
}
