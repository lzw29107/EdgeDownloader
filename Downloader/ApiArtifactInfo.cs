namespace EdgeDownloader.Downloader;

public class ApiArtifactInfo
{
    public string ArtifactName { get; set; }
    public string Location { get; set; }
    public string Hash { get; set; }
    public string HashAlgorithm { get; set; }
    public ulong SizeInBytes { get; set; }

    public ApiArtifactInfo()
    {
        ArtifactName = string.Empty;
        Location = string.Empty;
        Hash = string.Empty;
        HashAlgorithm = string.Empty;
        SizeInBytes = 0;
    }
}
