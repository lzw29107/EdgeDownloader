using System.Text.Json.Serialization;

namespace EdgeDownloader.Downloader;

public class DumpInfo : IComparable<DumpInfo>
{
    [JsonConverter(typeof(JsonStringEnumConverter<SupportedProduct>))]
    public SupportedProduct Product { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter<SupportedChannel>))]
    public SupportedChannel Channel { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter<SupportedOS>))]
    public SupportedOS OS { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter<SupportedArch>))]
    public SupportedArch Arch { get; set; }
    public Version Version { get; set; }
    public SortedSet<DownloadInfo> Links { get; set; }

    public DumpInfo()
    {
        Version = Constants.InvalidVersion;
        Links = [];
    }

    public DumpInfo(ProductInfo productInfo, SortedSet<DownloadInfo> downloadInfoSet)
    {
        Product = productInfo.EdgeType.Product;
        Channel = productInfo.EdgeType.Channel ?? SupportedChannel.Stable;
        OS = productInfo.EdgeType.OS;
        Arch = productInfo.EdgeType.Arch;
        Version = productInfo.Version;
        Links = downloadInfoSet;
    }

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

        result = other.Links.All(l => (l.Sha256 is null || l.Sha256.Length == 0)) ? 0 : 1;
        if (result != 0) return result;

        foreach (DownloadInfo link in Links)
        {
            result = other.Links.Any(l => l.CompareTo(link) == 0) ? 0 : 1;
            if (result != 0) return result;
        }

        return result;
    }

    public bool Equals(DumpInfo? other)
    {
        if (other is null)
            return false;
        return Product == other.Product &&
               Channel == other.Channel &&
               OS == other.OS &&
               Arch == other.Arch &&
               Version.Equals(other.Version) &&
               Links.SetEquals(other.Links);
    }

    public bool Includes(DumpInfo other)
    {
        if (Product == other.Product &&
            Channel == other.Channel &&
            OS == other.OS &&
            Arch == other.Arch &&
            Version == other.Version)
        {
            if ((OS == SupportedOS.Windows || OS == SupportedOS.Win7And8) && Product != SupportedProduct.EdgeEnterprise)
                return true;
            if (other.Links.All(l => Links.Any(link => link.Includes(l))))
                return true;
            return false;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Product, Channel, OS, Arch, Version, Links);
    }
}
