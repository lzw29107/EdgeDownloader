using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EdgeDownloader.Downloader;

public static partial class RequestHelper
{
    public static async Task<int> DumpEdgeProductAsync()
    {
        SortedSet<DumpInfo> dumpInfo = [];
        if (File.Exists("edge.json"))
        {
            string dumpJson = await File.ReadAllTextAsync("edge.json").ConfigureAwait(false);
            dumpInfo = JsonSerializer.Deserialize(dumpJson, CommonJsonContext.Default.SortedSetDumpInfo) ?? [];
        }

        SortedSet<ProductInfo> productInfoSet = await GetProductInfoSetAsync().ConfigureAwait(false);

        foreach (ProductInfo product in productInfoSet)
        {
            SortedSet<DownloadInfo> downloadInfoSet = [];
            if ((product.EdgeType.OS == SupportedOS.Windows || product.EdgeType.OS == SupportedOS.Win7And8)
                && product.EdgeType.Product != SupportedProduct.EdgeEnterprise)
            {
                Version version = product.Version;
                if (version == Constants.InvalidVersion)
                    version = await GetLatestVersionAsync(product.EdgeType).ConfigureAwait(false);

                foreach (ApiDownloadInfo apiDownloadInfo in await GetApiDownloadInfoSetAsync(product.EdgeType, version).ConfigureAwait(false))
                {
                    downloadInfoSet.Add(new(apiDownloadInfo.FileId, apiDownloadInfo.Url, Convert.FromBase64String(apiDownloadInfo.Hashes.Sha256)));
                }

                dumpInfo.RemoveWhere(d => d.Arch == product.EdgeType.Arch.ToString()
                    && d.Channel == product.EdgeType.Channel.ToString()
                    && d.OS == product.EdgeType.OS.ToString()
                    && d.Product == product.EdgeType.Product.ToString()
                    && d.Version == product.Version);
                dumpInfo.Add(new(product, downloadInfoSet));
            }
        }

        foreach (StandaloneProductType standaloneProduct in Constants.StandaloneProducts)
        {
            FileExtType fileType = Enum.GetValues<FileExtType>().Where(f => standaloneProduct.FwLinkId.ToString().Contains(f.ToString())).MaxBy(f => f.ToString().Length);

            string url = await GetFwLinkRedirectUrlAsync((int)standaloneProduct.FwLinkId).ConfigureAwait(false);

            dumpInfo.Add(new(new(standaloneProduct.EdgeType, GetVersionFromUrl(url)), [new(url[(url.LastIndexOf('/') + 1)..], url, null)]));
        }

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

                if (apiRelease.Artifacts.Count > 0)
                {
                    foreach (ApiArtifactInfo apiArtifact in apiRelease.Artifacts)
                    {
                        downloadInfoSet.Add(new(
                            apiArtifact.Location[(apiArtifact.Location.LastIndexOf('/') + 1)..],
                            apiArtifact.Location,
                            apiArtifact.HashAlgorithm == "SHA256" ? Convert.FromHexString(apiArtifact.Hash) : null));
                    }
                    dumpInfo.Add(new(new(edgeProductType, new(apiRelease.ProductVersion)), downloadInfoSet));
                }
            }
        }

        string json = JsonSerializer.Serialize(dumpInfo, CommonJsonContext.Default.SortedSetDumpInfo);
        await File.WriteAllTextAsync("edge.json", json).ConfigureAwait(false);
        return dumpInfo.Count;
    }

    public static async Task DownloadFileAsync(string url, string outputPath, byte[]? sha256Hash = null)
    {
        using HttpClient client = new()
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Failed to download file. Status code: {response.StatusCode}");
        byte[] fileBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        if (sha256Hash is not null && !sha256Hash.SequenceEqual(System.Security.Cryptography.SHA256.HashData(fileBytes)))
            throw new InvalidOperationException("SHA256 hash mismatch.");
        await File.WriteAllBytesAsync(outputPath, fileBytes).ConfigureAwait(false);
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
                downloadInfoSet.Add(new(apiDownloadInfo.FileId, apiDownloadInfo.Url, Convert.FromBase64String(apiDownloadInfo.Hashes.Sha256)));
            }
            if (downloadInfoSet.Count > 0)
                return downloadInfoSet;
        }

        foreach (StandaloneProductType standaloneProduct in Constants.StandaloneProducts)
        {
            if (standaloneProduct.EdgeType == productInfo.EdgeType && productInfo.Version == Constants.InvalidVersion)
            {
                FileExtType fileType = Enum.GetValues<FileExtType>().Where(f => standaloneProduct.FwLinkId.ToString().Contains(f.ToString())).MaxBy(f => f.ToString().Length);

                string url = await GetFwLinkRedirectUrlAsync((int)standaloneProduct.FwLinkId).ConfigureAwait(false);

                downloadInfoSet.Add(new(url[(url.LastIndexOf('/') + 1)..], url, null));
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

                if (edgeProductType != productInfo.EdgeType || (productInfo.Version != Constants.InvalidVersion) && (new Version(apiRelease.ProductVersion) != productInfo.Version))
                    continue;

                if (apiRelease.Artifacts.Count > 0)
                {
                    foreach (ApiArtifactInfo apiArtifact in apiRelease.Artifacts)
                    {
                        downloadInfoSet.Add(new(
                            apiArtifact.Location[(apiArtifact.Location.LastIndexOf('/') + 1)..],
                            apiArtifact.Location,
                            apiArtifact.HashAlgorithm == "SHA256" ? Convert.FromHexString(apiArtifact.Hash) : null));
                    }
                }
            }
        }

        downloadInfoSet.RemoveWhere(d =>
            downloadInfoSet.Any(down => down.FileName == d.FileName && down.Url == d.Url && down.Sha256 is not null & d.Sha256 is null)
        );

        return downloadInfoSet;
    }

    public static async Task<List<ApiUpdateInfo>> GetApiUpdateInfoListAsync(List<EdgeProductType> edgeProductTypes)
    {
        using HttpClient client = new();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, $"{Constants.EdgeApiBaseUrl}?action={RequestAction.BatchUpdates}");
        httpRequestMessage.Headers.Add("Accept", "application/json");
        httpRequestMessage.Content = new StringContent(
            JsonSerializer.Serialize([.. edgeProductTypes.Select(ept => new CdpRequestAttributes(ept))], CommonJsonContext.Default.ListCdpRequestAttributes),
            System.Text.Encoding.UTF8,
            "application/json");
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Failed to fetch update Info. Status code: {response.StatusCode}");

        string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        try
        {
            return JsonSerializer.Deserialize(responseBody, CommonJsonContext.Default.ListApiUpdateInfo) ?? throw new InvalidOperationException("Deserialization returned null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to deserialize update Info.", ex);
        }
    }

    public static async Task<bool> IsVersionAvailableAsync(EdgeProductType productType, Version version)
    {
        using HttpClient client = new();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, $"{Constants.EdgeApiBaseUrl}/{productType}/versions/{version}");
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Failed to fetch response. Status code: {response.StatusCode}");

        return true;
    }

    public static async Task<List<ApiDownloadInfo>> GetApiDownloadInfoSetAsync(EdgeProductType productType, Version version)
    {
        using HttpClient client = new();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, $"{Constants.EdgeApiBaseUrl}/{productType}/versions/{version}/files?action={RequestAction.GenerateDownloadInfo}&foregroundPriority=true");
        httpRequestMessage.Headers.Add("Accept", "application/json");
        httpRequestMessage.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Failed to fetch download Info. Status code: {response.StatusCode}");

        string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        try
        {
            return JsonSerializer.Deserialize(responseBody, CommonJsonContext.Default.ListApiDownloadInfo) ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to deserialize download Info.", ex);
        }
    }

    public static async Task<string> GetFwLinkRedirectUrlAsync(int id)
    {
        using HttpClientHandler handler = new() { AllowAutoRedirect = false };
        using HttpClient client = new(handler);
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, Constants.FwLinkBaseUrl + id);
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Redirect)
            throw new HttpRequestException($"Failed to fetch redirect URL. Status code: {response.StatusCode}");

        return response.Headers.Location?.ToString() ?? throw new InvalidOperationException("No redirect URL found in response headers.");
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
        using HttpClient client = new();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, Constants.EdgeUpdateApiProductsUrl);
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Failed to fetch product Info. Status code: {response.StatusCode}");

        string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        try
        {
            return JsonSerializer.Deserialize(responseBody, CommonJsonContext.Default.ListApiProductInfo) ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to deserialize product Info.", ex);
        }
    }

    public static async Task<SortedSet<ProductInfo>> GetBasicProductInfoSetAsync()
    {
        SortedSet<ProductInfo> productInfoSet = [];
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
                productInfoSet.Add(new ProductInfo(edgeProductType, new(apiRelease.ProductVersion)));
                if (edgeProductType.Product == SupportedProduct.EdgeEnterprise)
                    productInfoSet.Add(new ProductInfo(new EdgeProductType(SupportedProduct.Edge, edgeProductType.Channel, edgeProductType.OS, edgeProductType.Arch), new(apiRelease.ProductVersion)));
            }
        }
        return productInfoSet;
    }

    public static async Task<Version> GetLatestWinEdgeCanaryVersionAsync()
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

        EdgeProductType edgeProductWinCanaryX64 = new(SupportedProduct.Edge, SupportedChannel.Canary, SupportedOS.Windows, SupportedArch.X64);
        Version newVersion = new(version.Major, version.Minor, version.Build + 1, version.Revision);
        while (await IsVersionAvailableAsync(edgeProductWinCanaryX64, newVersion).ConfigureAwait(false))
        {
            newVersion = new Version(newVersion.Major, newVersion.Minor, newVersion.Build + 1, newVersion.Revision);
        }

        version = new Version(newVersion.Major, newVersion.Minor, newVersion.Build - 1, newVersion.Revision);
        newVersion = new Version(newVersion.Major + 1, newVersion.Minor, newVersion.Build, newVersion.Revision);
        while (await IsVersionAvailableAsync(edgeProductWinCanaryX64, newVersion).ConfigureAwait(false))
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

        EdgeProductType edgeProductWinCanaryX64 = new(SupportedProduct.Edge, SupportedChannel.Canary, SupportedOS.Windows, SupportedArch.X64);
        EdgeProductType edgeProductWinCanaryX86 = new(SupportedProduct.Edge, SupportedChannel.Canary, SupportedOS.Windows, SupportedArch.X86);
        EdgeProductType edgeProductWinCanaryArm64 = new(SupportedProduct.Edge, SupportedChannel.Canary, SupportedOS.Windows, SupportedArch.Arm64);

        Version canaryVersion = await GetLatestWinEdgeCanaryVersionAsync().ConfigureAwait(false);
        productInfoSet.Add(new ProductInfo(edgeProductWinCanaryX64, canaryVersion));
        productInfoSet.Add(new ProductInfo(edgeProductWinCanaryX86, canaryVersion));
        productInfoSet.Add(new ProductInfo(edgeProductWinCanaryArm64, canaryVersion));

        return productInfoSet;
    }

    [GeneratedRegex(@"\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5}")]
    private static partial Regex VersionRegex();
}
