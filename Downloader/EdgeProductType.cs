namespace EdgeDownloader.Downloader;

public class EdgeProductType : IComparable<EdgeProductType>
{
    public SupportedProduct Product { get; set; }
    public SupportedChannel? Channel { get; set; }
    public SupportedOS OS { get; set; }
    public SupportedArch Arch { get; set; }

    public EdgeProductType()
    {
        Product = SupportedProduct.Edge;
        OS = SupportedOS.Windows;
        Arch = SupportedArch.X64;
    }

    public EdgeProductType(SupportedProduct product, SupportedChannel? channel, SupportedOS os, SupportedArch arch)
    {
        Product = product;
        Channel = channel;
        OS = os;
        Arch = arch;
    }

    public static bool operator ==(EdgeProductType? left, EdgeProductType? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null)
            return false;
        return left.Product == right.Product && left.Channel == right.Channel && left.OS == right.OS && left.Arch == right.Arch;
    }

    public static bool operator !=(EdgeProductType? left, EdgeProductType? right)
    {
        return !(left == right);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not EdgeProductType other)
            return false;
        return this == other;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Product, Channel, OS, Arch);
    }

    public override string ToString()
    {
        string product = $"ms{Product.ToString().ToLowerInvariant()}";
        string os = OS.ToString().ToLowerInvariant();
        if (os == "windows")
            os = "win";
        return $"{product}-{Channel?.ToString().ToLowerInvariant()}-{os}-{Arch.ToString().ToLowerInvariant()}";
    }

    public int CompareTo(EdgeProductType? other)
    {
        if (other is null)
            return 1;

        int result = Product.CompareTo(other.Product);
        if (result != 0) return result;

        if (Channel.HasValue && other.Channel.HasValue)
        {
            result = Channel.Value.CompareTo(other.Channel.Value);
            if (result != 0) return result;
        }
        else if (Channel.HasValue)
        {
            return 1;
        }
        else if (other.Channel.HasValue)
        {
            return -1;
        }

        result = OS.CompareTo(other.OS);
        if (result != 0) return result;

        result = Arch.CompareTo(other.Arch);
        return result;
    }
}
