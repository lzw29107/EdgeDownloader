using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EdgeDownloader.Downloader;

public static partial class RequestHelper
{
    private static readonly HttpClient CommonHttpClient = new(new HttpClientHandler() { AllowAutoRedirect = false });
    private static SortedSet<ProductInfo>? BasicProductInfoSet;

    private static SortedSet<DumpInfo> LoadExistingDumpInfoSet()
    {
        if (File.Exists("edge.json"))
        {
            string dumpJson = File.ReadAllText("edge.json");
            return JsonSerializer.Deserialize(dumpJson, CommonJsonContext.Default.SortedSetDumpInfo) ?? [];
        }
        return [];
    }

    public static async Task<int> DumpEdgeProductAsync()
    {
        SortedSet<DumpInfo> dumpInfoSet = LoadExistingDumpInfoSet();

        SortedSet<ProductInfo> productInfoSet = await GetProductInfoSetAsync().ConfigureAwait(false);

        IEnumerable<Task<DumpInfo>> tasks = productInfoSet.Where(p => (p.EdgeType.OS == SupportedOS.Windows || p.EdgeType.OS == SupportedOS.Win7And8) && p.EdgeType.Product != SupportedProduct.EdgeEnterprise)
            .Select(product => Task.Run(() => DumpApiWinProductAsync(product)));

        SortedSet<DumpInfo> newDumpInfoSet = [.. await Task.WhenAll(tasks).ConfigureAwait(false)];

        foreach (ApiProductInfo apiProduct in await GetApiProductInfoListAsync().ConfigureAwait(false))
        {
            foreach (ApiReleaseInfo apiRelease in apiProduct.Releases)
            {
                SortedSet<DownloadInfo> downloadInfoSet = [];
                EdgeProductType edgeProductType = new();
                if (!Enum.TryParse(apiProduct.Product, true, out SupportedProduct product))
                {
                    product = (apiRelease.Artifacts.Count > 0 && apiRelease.Platform == "Windows") ? SupportedProduct.EdgeEnterprise : SupportedProduct.Edge;
                    if (!Enum.TryParse(apiProduct.Product, true, out SupportedChannel channel))
                        throw new InvalidOperationException($"Invalid product: {apiProduct.Product}");
                    edgeProductType.Channel = channel;
                }
                if (product == SupportedProduct.EdgeUpdate)
                    edgeProductType.Channel = SupportedChannel.Stable;
                if (!Enum.TryParse(apiRelease.Platform, true, out SupportedOS os))
                    throw new InvalidOperationException($"Invalid OS: {apiRelease.Platform}");
                edgeProductType.OS = os;

                if (!Enum.TryParse(apiRelease.Architecture, true, out SupportedArch arch))
                    throw new InvalidOperationException($"Invalid architecture: {apiRelease.Architecture}");
                edgeProductType.Arch = arch;

                if (apiRelease.Artifacts.Count == 0)
                    continue;

                foreach (ApiArtifactInfo apiArtifact in apiRelease.Artifacts)
                {
                    downloadInfoSet.Add(new(
                        apiArtifact.Location[(apiArtifact.Location.LastIndexOf('/') + 1)..],
                        apiArtifact.Location,
                        apiArtifact.HashAlgorithm == "SHA256" ? apiArtifact.Hash : string.Empty));
                }

                DumpInfo dump = new(new(edgeProductType, apiRelease.ProductVersion), downloadInfoSet);

                newDumpInfoSet.Add(dump);
            }
        }

        dumpInfoSet.UnionWith(await Task.WhenAll(Constants.StandaloneProducts.Select(s => DumpStandaloneProductAsync(s))).ConfigureAwait(false));

        foreach (DumpInfo dump in dumpInfoSet)
        {
            if (!newDumpInfoSet.Any(d => d.Includes(dump)))
                newDumpInfoSet.Add(dump);
        }

        string json = JsonSerializer.Serialize(newDumpInfoSet, CommonJsonContext.Default.SortedSetDumpInfo);
        await File.WriteAllTextAsync("edge.json", json).ConfigureAwait(false);
        return newDumpInfoSet.Count;
    }

    public static async Task<DumpInfo> DumpApiWinProductAsync(ProductInfo product)
    {
        Version version = product.Version;
        if (version == Constants.InvalidVersion)
            version = await GetLatestVersionAsync(product.EdgeType).ConfigureAwait(false);

        SortedSet<DownloadInfo> downloadInfoSet = [.. (await GetApiDownloadInfoSetAsync(product.EdgeType, version).ConfigureAwait(false))
            .Select(apiDownloadInfo => new DownloadInfo(
                apiDownloadInfo.FileId,
                apiDownloadInfo.Url,
                Convert.ToHexString(Convert.FromBase64String(apiDownloadInfo.Hashes.Sha256))
            ))];

        return new(product, downloadInfoSet);
    }

    public static async Task<DumpInfo> DumpStandaloneProductAsync(StandaloneProductType standaloneProduct)
    {
        string url = await GetFwLinkRedirectUrlAsync((int)standaloneProduct.FwLinkId).ConfigureAwait(false);

        ProductInfo product = new(standaloneProduct.EdgeType, GetVersionFromUrl(url));
        SortedSet<DownloadInfo> links = [new(url[(url.LastIndexOf('/') + 1)..], url, string.Empty)];

        return new(product, links);
    }

    public static async Task DownloadFileAsync(string url, string outputPath, byte[]? sha256Hash)
    {
        using HttpResponseMessage response = await CommonHttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using FileStream fileStream = File.Create(outputPath);
        await response.Content.CopyToAsync(fileStream);

        if (sha256Hash is not null)
        {
            fileStream.Position = 0;
            using SHA256 sha256 = SHA256.Create();
            if (!sha256.ComputeHash(fileStream).SequenceEqual(sha256Hash))
                throw new InvalidOperationException("SHA256 hash mismatch.");
        }
    }

    public static async Task<SortedSet<DownloadInfo>> GetProductDownloadInfoSetAsync(ProductInfo productInfo)
    {
        SortedSet<DownloadInfo> downloadInfoSet = [];

        if ((productInfo.EdgeType.OS == SupportedOS.Windows || productInfo.EdgeType.OS == SupportedOS.Win7And8)
            && productInfo.EdgeType.Product != SupportedProduct.EdgeEnterprise)
        {
            Version version = productInfo.Version;
            if (version == Constants.InvalidVersion)
                version = await GetLatestVersionAsync(productInfo.EdgeType).ConfigureAwait(false);

            foreach (ApiDownloadInfo apiDownloadInfo in await GetApiDownloadInfoSetAsync(productInfo.EdgeType, version).ConfigureAwait(false))
            {
                downloadInfoSet.Add(new(apiDownloadInfo.FileId, apiDownloadInfo.Url, Convert.ToHexString(Convert.FromBase64String(apiDownloadInfo.Hashes.Sha256))));
            }
            if (downloadInfoSet.Count > 0)
                return downloadInfoSet;
        }

        foreach (StandaloneProductType standaloneProduct in Constants.StandaloneProducts)
        {
            if (standaloneProduct.EdgeType == productInfo.EdgeType)
            {
                string url = await GetFwLinkRedirectUrlAsync((int)standaloneProduct.FwLinkId).ConfigureAwait(false);

                if (productInfo.Version == Constants.InvalidVersion || productInfo.Version == GetVersionFromUrl(url))
                    downloadInfoSet.Add(new(url[(url.LastIndexOf('/') + 1)..], url, string.Empty));
            }
        }

        foreach (ApiProductInfo apiProduct in await GetApiProductInfoListAsync().ConfigureAwait(false))
        {
            foreach (ApiReleaseInfo apiRelease in apiProduct.Releases)
            {
                EdgeProductType edgeProductType = new();
                if (!Enum.TryParse(apiProduct.Product, true, out SupportedProduct product))
                {
                    product = (apiRelease.Artifacts.Count > 0 && apiRelease.Platform == "Windows") ? SupportedProduct.EdgeEnterprise : SupportedProduct.Edge;
                    if (!Enum.TryParse(apiProduct.Product, true, out SupportedChannel channel))
                        throw new InvalidOperationException($"Invalid product: {apiProduct.Product}");
                    edgeProductType.Channel = channel;
                }
                if (product == SupportedProduct.EdgeUpdate)
                    edgeProductType.Channel = SupportedChannel.Stable;
                if (product != productInfo.EdgeType.Product)
                    continue;
                if (!Enum.TryParse(apiRelease.Platform, true, out SupportedOS os))
                    throw new InvalidOperationException($"Invalid OS: {apiRelease.Platform}");
                edgeProductType.OS = os;

                if (!Enum.TryParse(apiRelease.Architecture, true, out SupportedArch arch))
                    throw new InvalidOperationException($"Invalid architecture: {apiRelease.Architecture}");
                edgeProductType.Arch = arch;

                if (edgeProductType != productInfo.EdgeType || (productInfo.Version != Constants.InvalidVersion && apiRelease.ProductVersion != productInfo.Version))
                    continue;

                if (apiRelease.Artifacts.Count > 0)
                {
                    foreach (ApiArtifactInfo apiArtifact in apiRelease.Artifacts)
                    {
                        downloadInfoSet.Add(new(
                            apiArtifact.Location[(apiArtifact.Location.LastIndexOf('/') + 1)..],
                            apiArtifact.Location,
                            apiArtifact.HashAlgorithm == "SHA256" ? apiArtifact.Hash : string.Empty));
                    }
                }
            }
        }

        downloadInfoSet.RemoveWhere(d =>
            downloadInfoSet.Any(down => (!down.Equals(d) && down.Includes(d)))
        );

        return downloadInfoSet;
    }

    public static async Task<List<ApiUpdateInfo>> GetApiUpdateInfoListAsync(List<EdgeProductType> edgeProductTypes)
    {
        string url = $"{Constants.EdgeApiBaseUrl}?action={RequestAction.BatchUpdates}";

        using HttpResponseMessage response = await CommonHttpClient.PostAsJsonAsync(url, [.. edgeProductTypes.Select(ept => new CdpRequestAttributes(ept))], CommonJsonContext.Default.ListCdpRequestAttributes);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(CommonJsonContext.Default.ListApiUpdateInfo) ?? [];
    }

    public static async Task<bool> IsVersionAvailableAsync(EdgeProductType productType, Version version)
    {
        string url = $"{Constants.EdgeApiBaseUrl}/{productType}/versions/{version}";

        using HttpResponseMessage response = await CommonHttpClient.GetAsync(url);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Failed to check version availability. Status code: {response.StatusCode}");

        return true;
    }

    public static async Task<List<ApiDownloadInfo>> GetApiDownloadInfoSetAsync(EdgeProductType productType, Version version)
    {
        string url = $"{Constants.EdgeApiBaseUrl}/{productType}/versions/{version}/files?action={RequestAction.GenerateDownloadInfo}&foregroundPriority=true";
        using HttpResponseMessage response = await CommonHttpClient.PostAsJsonAsync(url, new ApiDownloadRequestInfo(), CommonJsonContext.Default.ApiDownloadRequestInfo);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(CommonJsonContext.Default.ListApiDownloadInfo) ?? [];
    }

    public static async Task<string> GetFwLinkRedirectUrlAsync(int id)
    {
        using HttpResponseMessage response = await CommonHttpClient.GetAsync(Constants.FwLinkBaseUrl + id);
        if (response.StatusCode != HttpStatusCode.Redirect)
            throw new HttpRequestException($"Failed to fetch redirect URL. Status code: {response.StatusCode}");

        return response.Headers.Location?.ToString() ?? throw new InvalidOperationException("No redirect URL found.");
    }

    public static Version? GetVersionFromUrl(string url)
    {
        Match match = VersionRegex().Match(url);
        string? versionString = match.Success ? match.Value : null;
        if (string.IsNullOrEmpty(versionString))
            return null;
        if (Version.TryParse(versionString, out Version? version))
            return version;
        return null;
    }

    public static async Task<Version?> GetVersionFromFwLinkIdAsync(int id)
    {
        string url = await GetFwLinkRedirectUrlAsync(id).ConfigureAwait(false);

        return GetVersionFromUrl(url);
    }

    public static async Task<Version> GetLatestVersionAsync(EdgeProductType edgeType)
    {
        bool basicInclude = edgeType.Product != SupportedProduct.EdgeWebView2 &&
            (edgeType.OS != SupportedOS.Android || edgeType.Arch != SupportedArch.Arm) &&
            (edgeType.OS != SupportedOS.Windows || edgeType.Channel != SupportedChannel.Canary) &&
            (edgeType.OS != SupportedOS.WCOS);
        SortedSet<ProductInfo> products = basicInclude
            ? await GetBasicProductInfoSetAsync().ConfigureAwait(false)
            : await GetProductInfoSetAsync().ConfigureAwait(false);
        foreach (ProductInfo product in products)
        {
            if (product.EdgeType == edgeType)
                return product.Version;
        }

        List<ApiUpdateInfo> apiUpdateInfo = await GetApiUpdateInfoListAsync([edgeType]).ConfigureAwait(false);
        if (apiUpdateInfo.Count > 0)
            return apiUpdateInfo[0].ContentId.Version;

        return Constants.InvalidVersion;
    }

    public static async Task<List<ApiProductInfo>> GetApiProductInfoListAsync()
    {
        return await CommonHttpClient.GetFromJsonAsync(Constants.EdgeUpdateApiProductsUrl, CommonJsonContext.Default.ListApiProductInfo) ?? [];
    }

    public static async Task<SortedSet<ProductInfo>> GetBasicProductInfoSetAsync()
    {
        if (BasicProductInfoSet is not null)
            return BasicProductInfoSet;
        BasicProductInfoSet = [];
        foreach (ApiProductInfo apiProduct in await GetApiProductInfoListAsync().ConfigureAwait(false))
        {
            foreach (ApiReleaseInfo apiRelease in apiProduct.Releases)
            {
                EdgeProductType edgeProductType = new();
                if (!Enum.TryParse(apiProduct.Product, true, out SupportedProduct product))
                {
                    product = (apiRelease.Artifacts.Count > 0 && apiRelease.Platform == "Windows") ? SupportedProduct.EdgeEnterprise : SupportedProduct.Edge;
                    if (!Enum.TryParse(apiProduct.Product, true, out SupportedChannel channel))
                    {
                        throw new InvalidOperationException($"Invalid product: {apiProduct.Product}");
                    }
                    else
                    {
                        edgeProductType.Channel = channel;
                    }
                }
                edgeProductType.Product = product;
                if (product == SupportedProduct.EdgeUpdate)
                    edgeProductType.Channel = SupportedChannel.Stable;
                if (!Enum.TryParse(apiRelease.Platform, true, out SupportedOS os))
                {
                    throw new InvalidOperationException($"Invalid OS: {apiRelease.Platform}");
                }
                else
                {
                    edgeProductType.OS = os;
                }
                if (!Enum.TryParse(apiRelease.Architecture, true, out SupportedArch arch))
                {
                    throw new InvalidOperationException($"Invalid architecture: {apiRelease.Architecture}");
                }
                else
                {
                    edgeProductType.Arch = arch;
                }
                BasicProductInfoSet.Add(new ProductInfo(edgeProductType, apiRelease.ProductVersion));
                if (edgeProductType.Product == SupportedProduct.EdgeEnterprise)
                    BasicProductInfoSet.Add(new ProductInfo(new EdgeProductType(SupportedProduct.Edge, edgeProductType.Channel, edgeProductType.OS, edgeProductType.Arch), apiRelease.ProductVersion));
            }
        }
        return BasicProductInfoSet;
    }

    public static async Task<Version> GetLatestWinEdgeCanaryVersionAsync(SupportedArch arch)
    {
        Version version = Constants.InvalidVersion;
        SortedSet<ProductInfo> products = await GetBasicProductInfoSetAsync().ConfigureAwait(false);
        foreach (ProductInfo product in products)
        {
            if (product.EdgeType.Product == SupportedProduct.Edge && product.EdgeType.Channel == SupportedChannel.Canary)
                version = product.Version;
        }

        if (version == Constants.InvalidVersion)
        {
            List<ApiUpdateInfo> apiUpdateInfo = await GetApiUpdateInfoListAsync([new EdgeProductType(SupportedProduct.Edge, SupportedChannel.Canary, SupportedOS.Windows, SupportedArch.X64)]).ConfigureAwait(false);
            if (apiUpdateInfo.Count > 0)
                version = apiUpdateInfo[0].ContentId.Version;
        }

        if (version == Constants.InvalidVersion)
            throw new InvalidOperationException("Failed to retrieve the latest Edge Canary version for Windows.");

        EdgeProductType edgeProductWinCanary = new(SupportedProduct.Edge, SupportedChannel.Canary, SupportedOS.Windows, arch);
        Version newVersion = new(version.Major, version.Minor, version.Build + 1, version.Revision);
        while (await IsVersionAvailableAsync(edgeProductWinCanary, newVersion).ConfigureAwait(false))
        {
            newVersion = new Version(newVersion.Major, newVersion.Minor, newVersion.Build + 1, newVersion.Revision);
        }

        version = new Version(newVersion.Major, newVersion.Minor, newVersion.Build - 1, newVersion.Revision);
        newVersion = new Version(newVersion.Major + 1, newVersion.Minor, newVersion.Build, newVersion.Revision);
        while (await IsVersionAvailableAsync(edgeProductWinCanary, newVersion).ConfigureAwait(false))
        {
            newVersion = new Version(newVersion.Major, newVersion.Minor, newVersion.Build + 1, newVersion.Revision);
        }

        if (new Version(newVersion.Major - 1, newVersion.Minor, newVersion.Build - 1, newVersion.Revision) == version)
        {
            return version;
        }
        else
        {
            return new Version(newVersion.Major, newVersion.Minor, newVersion.Build - 1, newVersion.Revision);
        }
    }

    public static async Task<SortedSet<ProductInfo>> GetProductInfoSetAsync()
    {
        SortedSet<ProductInfo> productInfoSet = await GetBasicProductInfoSetAsync().ConfigureAwait(false);

        IEnumerable<Task<ProductInfo>> tasks = Constants.BasicStandaloneProducts.Select(async standaloneProduct =>
        {
            Version? version = standaloneProduct.EdgeType.Product != SupportedProduct.EdgeEnterprise
                ? await GetVersionFromFwLinkIdAsync((int)standaloneProduct.FwLinkId).ConfigureAwait(false)
                : null;
            return new ProductInfo(
                standaloneProduct.EdgeType,
                version);
        });

        ProductInfo[] results = await Task.WhenAll(tasks).ConfigureAwait(false);
        foreach (ProductInfo product in results)
        {
            if (productInfoSet.All(p => (p.EdgeType != product.EdgeType)) && product.Version != Constants.InvalidVersion)
                productInfoSet.Add(product);
        }

        foreach (SupportedArch arch in new[] { SupportedArch.X64, SupportedArch.X86, SupportedArch.Arm64 })
        {
            Version canaryVersion = await GetLatestWinEdgeCanaryVersionAsync(arch).ConfigureAwait(false);
            productInfoSet.Add(new ProductInfo(new(SupportedProduct.Edge, SupportedChannel.Canary, SupportedOS.Windows, arch), canaryVersion));
        }

        return productInfoSet;
    }

    [GeneratedRegex(@"\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5}")]
    private static partial Regex VersionRegex();
}
