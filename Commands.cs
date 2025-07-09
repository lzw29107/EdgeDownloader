using System.Text.RegularExpressions;
using EdgeDownloader.Downloader;

namespace EdgeDownloader;

internal static partial class Commands
{
    internal static void Usage(int exitCode = 0)
    {
        Console.WriteLine("Usage: EdgeDownloader <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Note: Both '/' and '--' are supported as command prefixes.");
        Console.WriteLine("Commands:");
        Console.WriteLine("  /download         Download the latest or specified Edge version.");
        Console.WriteLine("  /list             List the latest available Edge versions.");
        Console.WriteLine("  /dump             Dump the Edge product Info to edge.json.");
        Console.WriteLine("  /help             Show this help message.");
        Console.WriteLine();
        Console.WriteLine("Download Options:");
        Console.WriteLine("  /product:<name>   Edge product type (Edge, EdgeUpdate, EdgeEnterprise, EdgeWebView2)");
        Console.WriteLine("  /channel:<name>   Edge channel (Stable, Beta, Dev, Canary)");
        Console.WriteLine("  /version:<ver>    Edge version (e.g., 140.0.3436.0; latest if omitted)");
        Console.WriteLine("  /os:<os>          Operating system (Windows, Win7And8, MacOS, Linux, Android, WCOS)");
        Console.WriteLine("  /arch:<arch>      Architecture (x86, x64, arm, arm64, universal)");
        Console.WriteLine("  /filetype:<type>  File type (exe, msi, deb, rpm, pkg, dmg, apk)");
        Console.WriteLine("  /output:<path>    Output directory (optional, defaults to current directory, if specified without path, it will only print the download links)");
        Console.WriteLine("    Default product: Edge, channel: Stable, os: Windows, arch: x64, filetype: exe");
        Console.WriteLine("  /all              Download all variants of the specified product.");
        Console.WriteLine("  /threads:<num>  Number of threads to use for downloading (default: 1, max: 64)");
        Console.WriteLine();
        Console.WriteLine("List Options:");
        Console.WriteLine("  /all              List all available products and versions");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  EdgeDownloader /download /product:Edge /channel:Stable /os:Windows /arch:x64");
        Console.WriteLine("  EdgeDownloader /list");
        Console.WriteLine("  EdgeDownloader /dump");
        Console.WriteLine("  EdgeDownloader /help");
        Console.WriteLine();
        Environment.Exit(exitCode);
    }

    internal static async Task DumpAsync()
    {
        Console.Write("Dumping Edge product Info...");
        int count = await RequestHelper.DumpEdgeProductAsync().ConfigureAwait(false);
        Console.WriteLine($"\rTotal Edge product Info dumped: {count}");
        Console.WriteLine("Edge product Info dumped to edge.json.");
    }

    internal static async Task DownloadAsync(ParsedArguments parsedArguments)
    {
        ProductInfo product = new(
            new EdgeProductType(
                parsedArguments.Product ?? SupportedProduct.Edge,
                parsedArguments.Channel,
                parsedArguments.OS ?? SupportedOS.Windows,
                parsedArguments.Arch ?? SupportedArch.X64),
            parsedArguments.Version);

        Console.Write("Generating download links...");

        SortedSet<DownloadInfo> downloadInfoSet = await RequestHelper.GetProductDownloadInfoSetAsync(product).ConfigureAwait(false);
        downloadInfoSet.RemoveWhere(d => d.GetFileType() != parsedArguments.FileType);

        Console.SetCursorPosition(0, Console.CursorTop);
        if (downloadInfoSet.Count == 0)
        {
            Console.WriteLine("Failed to generate download links.");
            return;
        }

        Console.WriteLine($"Total download links found: {downloadInfoSet.Count}");
        Console.WriteLine();

        bool hasDelta = downloadInfoSet.Any(di => DeltaVersionRegex().IsMatch(di.FileName));
        foreach (DownloadInfo downloadInfo in downloadInfoSet)
        {
            FileExtType? fileType = downloadInfo.GetFileType();
            if (downloadInfo.GetFileType() == parsedArguments.FileType)
            {
                bool recommended = downloadInfoSet.Count == 1 ||
                    (hasDelta && !DeltaVersionRegex().IsMatch(downloadInfo.FileName)) ||
                    (fileType == FileExtType.Dmg && !downloadInfo.FileName.Contains("_cn")) ||
                    (fileType == FileExtType.Apk &&
                    (parsedArguments.Arch == SupportedArch.Arm64 && downloadInfo.FileName.Contains("Apkpure")) ||
                    (parsedArguments.Arch == SupportedArch.Arm && downloadInfo.FileName.Contains("Tencent"))); // Just temporary workaround for Edge Stable Android links

                if (parsedArguments.OnlyPrintLinks)
                {
                    Console.WriteLine(downloadInfo.FileName + (recommended ? " (Recommended)" : string.Empty));
                    Console.WriteLine(downloadInfo.Url);
                    Console.WriteLine();
                }
                else
                {
                    if (!recommended && !parsedArguments.ListOrDownloadAll)
                        continue;
                    string outputPath = Path.Combine(parsedArguments.OutputDirectory, downloadInfo.FileName);
                    Console.WriteLine($"Downloading {downloadInfo.FileName} to {outputPath} ...");
                    await RequestHelper.DownloadFileAsync(downloadInfo.Url, outputPath, downloadInfo.Sha256, parsedArguments.DownloadMaxThreads).ConfigureAwait(false);
                }
            }
        }
    }

    internal static async Task ListAsync(bool all)
    {
        Console.WriteLine("Note:");
        Console.WriteLine("    iOS version of Edge is not supported by this tool.");
        Console.WriteLine("    Microsoft only provides Edge Stable Android links for third-party stores like APKPure and Tencent, so only these links are available.");
        Console.WriteLine("    If more than one version of Edge Canary Windows in the list, the highest version is available, but not officially released yet.");
        Console.WriteLine();
        Console.Write("Retrieving product Info ...");
        SortedSet<ProductInfo> products = all ?
            await RequestHelper.GetProductInfoSetAsync().ConfigureAwait(false) :
            await RequestHelper.GetBasicProductInfoSetAsync().ConfigureAwait(false);

        Console.SetCursorPosition(0, Console.CursorTop);
        Console.WriteLine($"Total products found: {products.Count}           ");
        Console.WriteLine("Available products:");
        Console.WriteLine("Product\t\tChannel\t\tVersion\t\tOS\t\tArchitecture");
        Console.WriteLine();
        foreach (ProductInfo product in products)
        {
            Console.Write($"{product.EdgeType.Product}\t");
            if (product.EdgeType.Product == SupportedProduct.Edge)
                Console.Write('\t');
            Console.Write($"{product.EdgeType.Channel?.ToString() ?? "N/A"}\t\t");
            if (product.Version == Constants.InvalidVersion)
            {
                Console.Write("Unknown\t\t");
            }
            else
                Console.Write($"{product.Version}\t");
            Console.Write($"{product.EdgeType.OS}\t\t");
            Console.WriteLine(product.EdgeType.Arch.ToString().ToLowerInvariant());
        }
    }

    [GeneratedRegex(@"\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5}_\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5}")]
    private static partial Regex DeltaVersionRegex();
}
