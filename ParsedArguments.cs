namespace EdgeDownloader;

internal class ParsedArguments
{
    public AllowedCommand Command { get; private set; }
    public SupportedProduct? Product { get; private set; }
    public SupportedChannel? Channel { get; private set; }
    public Version? Version { get; private set; }
    public SupportedOS? OS { get; private set; }
    public SupportedArch? Arch { get; private set; }
    public FileExtType? FileType { get; private set; }
    public string OutputDirectory { get; private set; } = Environment.CurrentDirectory;
    public bool OnlyPrintLinks { get; private set; }
    public bool ListOrDownloadAll { get; private set; }
    public int DownloadMaxThreads { get; private set; } = 1;

    public ParsedArguments(string[] args)
    {
        if (args.Length == 0)
        {
            Command = AllowedCommand.None;
            return;
        }

        bool commandSet = false;

        foreach (string arg in args)
        {
            string argWithoutPrefix;
            if (arg.StartsWith('/'))
            {
                argWithoutPrefix = arg[1..];
            }
            else if (arg.StartsWith("--"))
            {
                argWithoutPrefix = arg[2..];
            }
            else
            {
                throw new ArgumentException($"Unknown argument '{arg}'.");
            }

            string[] parts = argWithoutPrefix.Split(':', 2);
            string key = parts[0];
            string? value = parts.Length == 2 ? parts[1] : null;

            switch (key.ToLowerInvariant())
            {
                case "h":
                case "?":
                    if (commandSet)
                    {
                        throw new InvalidOperationException($"{arg}: Only one command can be specified.");
                    }
                    Command = AllowedCommand.Help;
                    break;
                case "help":
                case "download":
                case "list":
                case "dump":
                    if (commandSet)
                    {
                        throw new InvalidOperationException($"{arg}: Only one command can be specified.");
                    }
                    Command = Enum.Parse<AllowedCommand>(key, true);
                    commandSet = true;
                    break;
                case "product":
                    if (!commandSet || Command != AllowedCommand.Download)
                        throw new InvalidOperationException($"{arg}: The product can only be specified with the download command.");
                    if (!Enum.TryParse(value, true, out SupportedProduct product))
                        throw new ArgumentException($"Invalid product type '{value}'. Valid options are: Edge, EdgeEnterprise, EdgeWebView2, EdgeUpdate.");
                    Product = product;
                    break;
                case "channel":
                    if (!commandSet || Command != AllowedCommand.Download)
                        throw new InvalidOperationException($"{arg}: The channel can only be specified with the download command.");
                    if (!Enum.TryParse(value, true, out SupportedChannel channel))
                        throw new ArgumentException($"Invalid channel '{value}'. Valid channels are: Stable, Beta, Dev, Canary.");
                    Channel = channel;
                    break;
                case "version":
                    if (!commandSet || Command != AllowedCommand.Download)
                        throw new InvalidOperationException($"{arg}: The version can only be specified with the download command.");
                    if (!Version.TryParse(value, out Version? version) || version is null)
                        throw new ArgumentException($"Invalid version '{value}'. Expected format is 'Major.Minor.Build.Revision'.");
                    Version = version;
                    break;
                case "os":
                    if (!commandSet || Command != AllowedCommand.Download)
                        throw new InvalidOperationException($"{arg}: The OS can only be specified with the download command.");
                    if (!Enum.TryParse(value, true, out SupportedOS os))
                        throw new ArgumentException($"Invalid OS '{value}'. Valid options are: Windows, Win7And8, MacOS, Linux, Android, iOS.");
                    OS = os;
                    break;
                case "arch":
                    if (!commandSet || Command != AllowedCommand.Download)
                        throw new InvalidOperationException($"{arg}: The architecture can only be specified with the download command.");
                    if (!Enum.TryParse(value, true, out SupportedArch arch))
                        throw new ArgumentException($"Invalid architecture '{value}'. Valid options are x86, x64, arm, arm64, universal.");
                    Arch = arch;
                    break;
                case "filetype":
                    if (!commandSet || Command != AllowedCommand.Download)
                        throw new InvalidOperationException($"{arg}: The file type can only be specified with the download command.");
                    if (!Enum.TryParse(value, true, out FileExtType fileType))
                        throw new ArgumentException($"Invalid file type '{value}'. Valid options are: exe, msi, deb, rpm, pkg, dmg, apk.");
                    FileType = fileType;
                    break;
                case "all":
                    if (!commandSet || (Command != AllowedCommand.Download && Command != AllowedCommand.List))
                        throw new InvalidOperationException($"{arg}: The 'all' option can only be specified with the download or list command.");
                    ListOrDownloadAll = true;
                    break;
                case "output":
                    if (!commandSet || Command != AllowedCommand.Download)
                        throw new InvalidOperationException($"{arg}: The output directory can only be specified with the download command.");
                    if (string.IsNullOrEmpty(value))
                    {
                        OnlyPrintLinks = true;
                        continue;
                    }
                    if (Directory.Exists(value))
                    {
                        OutputDirectory = Path.GetFullPath(value);
                    }
                    else
                    {
                        throw new ArgumentException($"Output directory '{value}' does not exist.");
                    }
                    break;
                case "threads":
                    if (!commandSet || Command != AllowedCommand.Download)
                        throw new InvalidOperationException($"{arg}: The number of threads can only be specified with the download command.");
                    if (!int.TryParse(value, out int threads) || threads <= 0 || threads > 64)
                        throw new ArgumentException($"Invalid number of threads '{value}'. Must be between 1 and 64.");
                    DownloadMaxThreads = threads;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{arg}'.");
            }
        }

        if (!commandSet)
        {
            throw new InvalidOperationException("No main command specified.");
        }

        Product ??= SupportedProduct.Edge;
        Channel ??= SupportedChannel.Stable;
        OS ??= SupportedOS.Windows;
        switch (Product)
        {
            case SupportedProduct.Edge:
                break;
            case SupportedProduct.EdgeEnterprise:
                FileType ??= FileExtType.Msi;
                if (OS != SupportedOS.Windows)
                    throw new ArgumentException($"Invalid OS '{OS}' for Product {Product}. Valid options are: Windows.");
                if (Channel == SupportedChannel.Canary)
                    throw new ArgumentException($"Invalid Channel '{Channel} for Product {Product}. Valid channels are: Stable, Beta, Dev.");
                if (FileType != FileExtType.Msi)
                    throw new ArgumentException($"Invalid file type '{FileType}' for Product {Product}. Valid options are: msi.");
                break;
            case SupportedProduct.EdgeWebView2:
                if (OS != SupportedOS.Windows)
                {
                    if (OS != SupportedOS.WCOS)
                        throw new ArgumentException($"Invalid OS '{OS}' for Product {Product}. Valid options are: Windows, WCOS.");
                    FileType ??= FileExtType.Msix;
                }
                else
                {
                    FileType ??= FileExtType.Exe;
                    if (FileType != FileExtType.Exe)
                        throw new ArgumentException($"Invalid file type '{FileType}' for Product {Product}. Valid options are: exe.");
                }
                break;
            case SupportedProduct.EdgeUpdate:
                FileType ??= FileExtType.Exe;
                Arch ??= SupportedArch.X86;
                if (OS != SupportedOS.Windows && OS != SupportedOS.Win7And8)
                    throw new ArgumentException($"Invalid OS '{OS}' for Product {Product}. Valid options are: Windows, Win7And8.");
                if (Channel != SupportedChannel.Stable)
                    throw new ArgumentException($"Invalid Channel '{Channel}' for Product {Product}. Valid channels are: Stable.");
                if (Arch != SupportedArch.X86)
                    throw new ArgumentException($"Invalid architecture '{Arch}' for Product {Product}. Valid options are: x86.");
                if (FileType != FileExtType.Exe)
                    throw new ArgumentException($"Invalid file type '{FileType}' for Product {Product}. Valid options are: exe.");
                break;
        }
        switch (OS)
        {
            case SupportedOS.Windows:
                FileType ??= FileExtType.Exe;
                Arch ??= SupportedArch.X64;
                if (Arch == SupportedArch.Arm || Arch == SupportedArch.Universal)
                    throw new ArgumentException($"Invalid architecture '{Arch}' for OS {OS}. Valid options are: x86, x64, arm64.");
                break;
            case SupportedOS.Win7And8:
                FileType ??= FileExtType.Exe;
                Arch ??= SupportedArch.X64;
                if (Arch != SupportedArch.X86 && Arch != SupportedArch.X64)
                    throw new ArgumentException($"Invalid architecture '{Arch}' for OS {OS}. Valid options are: x86, x64.");
                if (FileType != FileExtType.Exe)
                    throw new ArgumentException($"Invalid file type '{FileType}' for OS {OS}. Valid options are: exe.");
                break;
            case SupportedOS.WCOS:
                FileType ??= FileExtType.Msix; ;
                Arch ??= SupportedArch.Arm64;
                if (Arch != SupportedArch.Arm64)
                    throw new ArgumentException($"Invalid architecture '{Arch}' for OS {OS}. Valid options are: arm64.");
                if (FileType != FileExtType.Msix)
                    throw new ArgumentException($"Invalid file type '{FileType}' for OS {OS}. Valid options are: msix.");
                break;
            case SupportedOS.iOS:
                throw new ArgumentException($"Unsupported OS '{OS}'.");
            case SupportedOS.Android:
                Arch ??= SupportedArch.Arm64;
                FileType ??= FileExtType.Apk;
                if (Arch != SupportedArch.Arm && Arch != SupportedArch.Arm64)
                    throw new ArgumentException($"Invalid architecture '{Arch}' for OS {OS}. Valid options are: arm, arm64.");
                if (FileType != FileExtType.Apk)
                    throw new ArgumentException($"Invalid file type '{FileType}' for OS {OS}. Valid options are: apk.");
                if (Channel == SupportedChannel.Stable)
                    Console.WriteLine("Microsoft only provides Edge Stable Android links for third-party stores like APKPure and Tencent, so only these links are available.");
                break;
            case SupportedOS.Linux:
                Arch ??= SupportedArch.X64;
                FileType ??= FileExtType.Deb;
                if (Arch != SupportedArch.X64)
                    throw new ArgumentException($"Invalid architecture '{Arch}' for OS {OS}. Valid options are: x64.");
                if (Channel == SupportedChannel.Canary)
                    throw new ArgumentException($"Invalid Channel '{Channel} for OS {OS}. Valid channels are: Stable, Beta, Dev.");
                if (FileType != FileExtType.Deb && FileType != FileExtType.Rpm)
                    throw new ArgumentException($"Invalid file type '{FileType}' for OS {OS}. Valid options are: deb, rpm.");
                break;
            case SupportedOS.MacOS:
                Arch ??= SupportedArch.Universal;
                FileType ??= FileExtType.Pkg;
                if (Arch != SupportedArch.Universal)
                    throw new ArgumentException($"Invalid architecture '{Arch}' for OS {OS}. Valid options are: universal.");
                if (FileType != FileExtType.Pkg && FileType != FileExtType.Dmg)
                    throw new ArgumentException($"Invalid file type '{FileType}' for OS {OS}. Valid options are: pkg, dmg.");
                break;
        }
    }

    internal enum AllowedCommand
    {
        None,
        Download,
        List,
        Dump,
        Help
    }
}
