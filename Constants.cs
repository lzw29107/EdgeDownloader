using EdgeDownloader.Downloader;

namespace EdgeDownloader;

public static class Constants
{
    public static readonly string EdgeUpdateApiProductsUrl = "https://edgeupdates.microsoft.com/api/products";
    // API v1.1 url
    // public static readonly string EdgeApiBaseUrl = "https://msedge.api.cdp.microsoft.com/api/v1.1/internal/contents/Browser/namespaces/Default/names";
    public static readonly string EdgeApiBaseUrl = "https://msedge.api.cdp.microsoft.com/api/v2/contents/Browser/namespaces/Default/names";
    public static readonly string FwLinkBaseUrl = "https://go.microsoft.com/fwlink/?linkid=";
    public static readonly Version InvalidVersion = new(0, 0, 0, 0);

    public static readonly List<StandaloneProductType> StandaloneProducts = [.. Enum.GetValues<FwLinkId>().Select(fwLinkId =>
    {
        string IdProduct = fwLinkId.ToString();
        SupportedProduct product = Enum.GetValues<SupportedProduct>().Where(s => IdProduct.Contains(s.ToString())).MaxBy(s => s.ToString().Length);
        SupportedChannel channel = Enum.GetValues<SupportedChannel>().Single(s => IdProduct.Contains(s.ToString()));
        FileExtType fileType = Enum.GetValues<FileExtType>().Where(f => IdProduct.Contains(f.ToString())).MaxBy(f => f.ToString().Length);
        SupportedOS os = fileType switch
        {
            FileExtType.Exe or FileExtType.Msi => SupportedOS.Windows,
            FileExtType.Deb or FileExtType.Rpm => SupportedOS.Linux,
            FileExtType.Pkg or FileExtType.Dmg => SupportedOS.MacOS,
            FileExtType.Apk => SupportedOS.Android,
            FileExtType.Msix => SupportedOS.WCOS,
            _ => throw new NotImplementedException()
        };
        SupportedArch arch = Enum.GetValues<SupportedArch>().Where(s => IdProduct.Contains(s.ToString())).MaxBy(s => s.ToString().Length);

        return new StandaloneProductType(new EdgeProductType(product, channel, os, arch), fwLinkId);
    }).Where(s => !Enum.IsDefined(typeof(FwLinkId), $"{s.FwLinkId}New"))];

    public static readonly List<StandaloneProductType> BasicStandaloneProducts = [.. StandaloneProducts.Where(
        s => (s.EdgeType.OS != SupportedOS.Android ||
        s.EdgeType.Channel != SupportedChannel.Stable ||
        s.FwLinkId == FwLinkId.EdgeStableArmApkTencent ||
        s.FwLinkId == FwLinkId.EdgeStableArm64ApkApkpure))];
}