namespace EdgeDownloader.Downloader;

public class ProductInfo(EdgeProductType edgeType, Version? version) : IComparable<ProductInfo>
{
    public EdgeProductType EdgeType { get; set; } = edgeType;
    public Version Version { get; set; } = version ?? Constants.InvalidVersion;

    int IComparable<ProductInfo>.CompareTo(ProductInfo? other)
    {
        if (other is null) return 1;

        if (EdgeType is null && other.EdgeType is not null)
            return -1;
        if (EdgeType is not null && other.EdgeType is null)
            return 1;
        if (EdgeType is not null && other.EdgeType is not null)
        {
            int edgeTypeCompare = EdgeType.CompareTo(other.EdgeType);
            if (edgeTypeCompare != 0)
                return edgeTypeCompare;
        }

        return Version.CompareTo(other.Version);
    }
}
