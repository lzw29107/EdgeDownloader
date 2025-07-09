namespace EdgeDownloader.Downloader;

public class DumpInfo(ProductInfo productInfo, SortedSet<DownloadInfo> downloadInfoSet) : IComparable<DumpInfo>
{
	public string Product { get; set; } = productInfo.EdgeType.Product.ToString();
	public string Channel { get; set; } = productInfo.EdgeType.Channel.ToString() ?? string.Empty;
	public string OS { get; set; } = productInfo.EdgeType.OS.ToString();
	public string Arch { get; set; } = productInfo.EdgeType.Arch.ToString();
	public Version Version { get; set; } = productInfo.Version;
	public SortedSet<DownloadInfo> Links { get; set; } = downloadInfoSet;

	public int CompareTo(DumpInfo? other)
	{
		if (other is null)
			return 1;

		int result = Product.CompareTo(other.Product);
		if (result != 0) return result;

		result = OS.CompareTo(other.OS);
		if (result != 0) return result;

		result = Channel.CompareTo(other.Channel);
		if (result != 0) return result;

		result = Arch.CompareTo(other.Arch);
		if (result != 0) return result;

		result = Version.CompareTo(other.Version);
		if (result != 0) return result;

		result = Links.Count.CompareTo(other.Links.Count);
		if (result != 0) return result;

		foreach (DownloadInfo link in Links)
		{
			result = other.Links.Any(l => l.CompareTo(link) == 0) ? 0 : 1;
			if (result != 0) return result;
		}

		return result;
	}
}
