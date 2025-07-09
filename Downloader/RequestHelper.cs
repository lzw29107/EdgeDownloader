using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EdgeDownloader.Downloader;

public static partial class RequestHelper
{
    private static readonly HttpClient CommonHttpClient = new(new HttpClientHandler() { AllowAutoRedirect = false });
    private static readonly HttpClient DownloadHttpClient = new(new HttpClientHandler() { AllowAutoRedirect = false })
    {
        Timeout = Timeout.InfiniteTimeSpan
    };
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
            .Select(product => DumpApiWinProductAsync(product));

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
                        apiArtifact.HashAlgorithm == "SHA256" ? Convert.FromHexString(apiArtifact.Hash) : null));
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
                Convert.FromBase64String(apiDownloadInfo.Hashes.Sha256)
            ))];

        return new(product, downloadInfoSet);
    }

    public static async Task<DumpInfo> DumpStandaloneProductAsync(StandaloneProductType standaloneProduct)
    {
        string url = await GetFwLinkRedirectUrlAsync((int)standaloneProduct.FwLinkId).ConfigureAwait(false);

        ProductInfo product = new(standaloneProduct.EdgeType, GetVersionFromUrl(url));
        SortedSet<DownloadInfo> links = [new(url[(url.LastIndexOf('/') + 1)..], url, null)];

        return new(product, links);
    }

    private static async Task CopyStreamWithProgressAsync(Stream input, Stream output, ProgressTracker progressTracker)
    {
        byte[] buffer = new byte[8192];
        int bytesRead;
        while ((bytesRead = await input.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            await Task.Yield();
            progressTracker.UpdateProgress(bytesRead);
            await output.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
        }
    }

    public static async Task DownloadFileAsync(string url, string outputPath, byte[]? sha256Hash, int maxThreads)
    {
        using FileStream fileStream = new(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);

        if (maxThreads > 1)
        {
            PiecesHashInfo piecesHash = await CommonHttpClient.GetFromJsonAsync(GetPiecesHashUrl(url), CommonJsonContext.Default.PiecesHashInfo).ConfigureAwait(false) ?? throw new InvalidOperationException("Failed to retrieve pieces hash info.");

            fileStream.SetLength(piecesHash.ContentLength);
            await DownloadChunksAsync(url, outputPath, piecesHash, maxThreads).ConfigureAwait(false);
        }
        else
        {
            using HttpResponseMessage response = await DownloadHttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            long contentLength = response.Content.Headers.ContentLength ?? throw new InvalidOperationException("Content-Length header is missing.");
            fileStream.SetLength(contentLength);
            ProgressTracker progressTracker = new(contentLength);

            using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await CopyStreamWithProgressAsync(stream, fileStream, progressTracker).ConfigureAwait(false);
        }

        if (sha256Hash is not null && sha256Hash.Length == 32)
        {
            fileStream.Position = 0;
            if (!(await SHA256.HashDataAsync(fileStream).ConfigureAwait(false)).SequenceEqual(sha256Hash))
                throw new InvalidOperationException("SHA256 hash mismatch.");
        }
    }


    private static async Task DownloadChunksAsync(string url, string outputPath, PiecesHashInfo piecesHash, int maxThreads)
    {
        ProgressTracker progressTracker = new(piecesHash.ContentLength);
        Task[] downloadTasks = new Task[piecesHash.Pieces.Length];
        SemaphoreSlim semaphore = new(maxThreads);

        for (int i = 0; i < piecesHash.Pieces.Length; i++)
        {
            long startByte = i * piecesHash.PieceSize;
            long endByte = (i == piecesHash.Pieces.Length - 1)
                ? piecesHash.ContentLength - 1
                : startByte + piecesHash.PieceSize - 1;

            downloadTasks[i] = DownloadChunkAsync(url, outputPath, startByte, endByte, piecesHash.Pieces[i], progressTracker, semaphore);
        }

        await Task.WhenAll(downloadTasks).ConfigureAwait(false);
    }

    private static async Task DownloadChunkAsync(string url, string outputPath, long startByte, long endByte, byte[] hash, ProgressTracker progressTracker, SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();

        try
        {
            for (int retry = 0; retry < 5; retry++)
            {
                if (retry > 0)
                {
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.WriteLine("Warning: Retrying download chunk...");
                    progressTracker.UpdateProgress(startByte - endByte - 1);
                }
                HttpRequestMessage request = new(HttpMethod.Get, url);
                request.Headers.Range = new(startByte, endByte);

                using HttpResponseMessage response = await DownloadHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                using Stream chunkData = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                MemoryStream memoryStream = new((int)(endByte - startByte + 1));
                await CopyStreamWithProgressAsync(chunkData, memoryStream, progressTracker).ConfigureAwait(false);
                memoryStream.Seek(0, SeekOrigin.Begin);

                if ((await SHA256.HashDataAsync(memoryStream).ConfigureAwait(false)).SequenceEqual(hash))
                {
                    using FileStream fileStream = new(outputPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                    fileStream.Seek(startByte, SeekOrigin.Begin);
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    await memoryStream.CopyToAsync(fileStream).ConfigureAwait(false);
                    return;
                }
            }
            throw new InvalidOperationException("Failed to download chunk after multiple retries.");
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static async Task<SortedSet<DownloadInfo>> GetProductDownloadInfoSetAsync(ProductInfo productInfo)
    {
        SortedSet<DownloadInfo> downloadInfoSet = [];

        if ((productInfo.EdgeType.OS == SupportedOS.Windows || productInfo.EdgeType.OS == SupportedOS.Win7And8)
            && productInfo.EdgeType.Product != SupportedProduct.EdgeEnterprise)
        {
            EdgeProductType edgeType = productInfo.EdgeType;
            if (edgeType.Product == SupportedProduct.EdgeWebView2)
                edgeType.Product = SupportedProduct.Edge;

            Version version = productInfo.Version;
            if (version == Constants.InvalidVersion)
                version = await GetLatestVersionAsync(edgeType).ConfigureAwait(false);

            foreach (ApiDownloadInfo apiDownloadInfo in await GetApiDownloadInfoSetAsync(edgeType, version).ConfigureAwait(false))
            {
                downloadInfoSet.Add(new(apiDownloadInfo.FileId, apiDownloadInfo.Url, Convert.FromBase64String(apiDownloadInfo.Hashes.Sha256)));
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

                if (edgeProductType != productInfo.EdgeType || (productInfo.Version != Constants.InvalidVersion && apiRelease.ProductVersion != productInfo.Version))
                    continue;

                if (apiRelease.Artifacts.Count > 0)
                {
                    foreach (ApiArtifactInfo apiArtifact in apiRelease.Artifacts)
                    {
                        downloadInfoSet.Add(new(
                            apiArtifact.Location[(apiArtifact.Location.LastIndexOf('/') + 1)..],
                            apiArtifact.Location,
                            apiArtifact.HashAlgorithm == "SHA256" ?  Convert.FromHexString(apiArtifact.Hash) : null));
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

        using HttpResponseMessage response = await CommonHttpClient.PostAsJsonAsync(url, [.. edgeProductTypes.Select(ept => new CdpRequestAttributes(ept))], CommonJsonContext.Default.ListCdpRequestAttributes).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(CommonJsonContext.Default.ListApiUpdateInfo).ConfigureAwait(false) ?? [];
    }

    public static async Task<bool> IsVersionAvailableAsync(EdgeProductType productType, Version version)
    {
        string url = $"{Constants.EdgeApiBaseUrl}/{productType}/versions/{version}";

        using HttpResponseMessage response = await CommonHttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Failed to check version availability. Status code: {response.StatusCode}");

        return true;
    }

    public static async Task<List<ApiDownloadInfo>> GetApiDownloadInfoSetAsync(EdgeProductType productType, Version version)
    {
        string url = $"{Constants.EdgeApiBaseUrl}/{productType}/versions/{version}/files?action={RequestAction.GenerateDownloadInfo}&foregroundPriority=true";
        using HttpResponseMessage response = await CommonHttpClient.PostAsJsonAsync(url, new ApiDownloadRequestInfo(), CommonJsonContext.Default.ApiDownloadRequestInfo).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(CommonJsonContext.Default.ListApiDownloadInfo).ConfigureAwait(false) ?? [];
    }

    public static async Task<string> GetFwLinkRedirectUrlAsync(int id)
    {
        using HttpResponseMessage response = await CommonHttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, Constants.FwLinkBaseUrl + id)).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Redirect)
            throw new HttpRequestException($"Failed to fetch redirect URL. Status code: {response.StatusCode}");

        return response.Headers.Location?.ToString() ?? throw new InvalidOperationException("No redirect URL found.");
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
        return await CommonHttpClient.GetFromJsonAsync(Constants.EdgeUpdateApiProductsUrl, CommonJsonContext.Default.ListApiProductInfo).ConfigureAwait(false) ?? [];
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
            return new ProductInfo(standaloneProduct.EdgeType, version);
        }).Concat(
            new SupportedArch[] { SupportedArch.X64, SupportedArch.X86, SupportedArch.Arm64 }.Select(async arch =>
            {
                Version canaryVersion = await GetLatestWinEdgeCanaryVersionAsync(arch).ConfigureAwait(false);
                return new ProductInfo(new(SupportedProduct.Edge, SupportedChannel.Canary, SupportedOS.Windows, arch), canaryVersion);
            }
        ));

        foreach (ProductInfo product in await Task.WhenAll(tasks).ConfigureAwait(false))
        {
            if (productInfoSet.All(p => (p.EdgeType != product.EdgeType)) && product.Version != Constants.InvalidVersion)
                productInfoSet.Add(product);
        }
        return productInfoSet;
    }

    private static string GetPiecesHashUrl(string downloadUrl)
    {
        string hashUrl = downloadUrl.IndexOf('?') > 0
            ? downloadUrl[..downloadUrl.IndexOf('?')].Replace("tlu.", string.Empty)
            : downloadUrl;
        hashUrl += "/pieceshash";
        return hashUrl;
    }

    private static Version? GetVersionFromUrl(string url)
    {
        Match match = VersionRegex().Match(url);
        string? versionString = match.Success ? match.Value : null;
        if (string.IsNullOrEmpty(versionString))
            return null;
        if (Version.TryParse(versionString, out Version? version))
            return version;
        return null;
    }

    private static async Task<Version?> GetVersionFromFwLinkIdAsync(int id)
    {
        string url = await GetFwLinkRedirectUrlAsync(id).ConfigureAwait(false);

        return GetVersionFromUrl(url);
    }

    [GeneratedRegex(@"\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5}")]
    private static partial Regex VersionRegex();
}
