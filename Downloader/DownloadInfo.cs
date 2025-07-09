namespace EdgeDownloader.Downloader;

public class DownloadInfo(string fileName, string url, string sha256) : IComparable<DownloadInfo>
{
    public string FileName { get; set; } = fileName;
    public string Url { get; set; } = url;
    public string Sha256 { get; set; } = sha256;

    public FileExtType? GetFileType()
    {
        return Enum.TryParse(FileName[(FileName.LastIndexOf('.') + 1)..], true, out FileExtType fileType) ? fileType : null;
    }

    public int CompareTo(DownloadInfo? other)
    {
        if (other is null)
            return 1;

        int result = FileName.CompareTo(other.FileName);
        if (result != 0) return result;

        result = Url.CompareTo(other.Url);
        if (result != 0) return result;

        result = Sha256.CompareTo(other.Sha256);
        return result;
    }

    public bool Includes(DownloadInfo other)
    {
        return FileName == other.FileName && Url == other.Url && (Sha256 == other.Sha256 || other.Sha256 == string.Empty);
    }

    public bool Equals(DownloadInfo? other)
    {
        if (other is null)
            return false;
        return FileName == other.FileName && Url == other.Url && Sha256 == other.Sha256;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(FileName, Url, Sha256);
    }
}
