namespace EdgeDownloader.Downloader;

public class DownloadInfo(string fileName, string url, byte[]? sha256) : IComparable<DownloadInfo>
{
    public string FileName { get; set; } = fileName;
    public string Url { get; set; } = url;
    public byte[]? Sha256 { get; set; } = sha256;

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

        if (Sha256 is null && other.Sha256 is null)
            return 0;

        if (Sha256 is null || other.Sha256 is null)
            return Sha256 is null ? -1 : 1;

        return Sha256.SequenceEqual(other.Sha256) ? 0 : 1;
    }

    public bool Includes(DownloadInfo other)
    {
        if (other is null)
            return false;

        return FileName == other.FileName && Url == other.Url &&
           (other.Sha256 is null || Sha256?.SequenceEqual(other.Sha256) == true);
    }

    public bool Equals(DownloadInfo? other)
    {
        if (other is null)
            return false;
        return FileName == other.FileName && Url == other.Url && 
           other.Sha256 is not null ? Sha256?.SequenceEqual(other.Sha256) == true : Sha256 is null;
    }

    public override int GetHashCode()
    {
        int hashCode = HashCode.Combine(FileName, Url);
        if (Sha256 is not null)
            hashCode = HashCode.Combine(hashCode, Sha256.Length, Sha256);

        return hashCode;
    }
}
