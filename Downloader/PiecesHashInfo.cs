namespace EdgeDownloader.Downloader;

public class PiecesHashInfo
{
    public int MajorVersion { get; set; }
    public int MinorVersion { get; set; }
    public byte[] HashOfHashes { get; set; }
    public long ContentLength { get; set; }
    public long PieceSize { get; set; }
    public byte[][] Pieces { get; set; }

    public PiecesHashInfo()
    {
        HashOfHashes = [];
        Pieces = [];
    }
}
