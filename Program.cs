using System.Reflection;
using static EdgeDownloader.Commands;

namespace EdgeDownloader;

internal class Program
{
    static async Task Main(string[] args)
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? throw new InvalidOperationException("Failed to get the entry assembly.");
        AssemblyInformationalVersionAttribute version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>() ?? new AssemblyInformationalVersionAttribute(Constants.InvalidVersion.ToString());
        AssemblyDescriptionAttribute description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>() ?? new AssemblyDescriptionAttribute("No description available.");
        Console.WriteLine(description.Description);
        Console.WriteLine();
        Console.WriteLine("https://github.com/lzw29107/EdgeDownloader");
        Console.WriteLine($"Version {version.InformationalVersion}");
        Console.WriteLine();

        ParsedArguments parsedArgs;
        try
        {
            parsedArgs = new ParsedArguments(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing arguments: {ex.Message}");
            Console.WriteLine();
            Usage(1);
            return;
        }

        switch (parsedArgs.Command)
        {
            case ParsedArguments.AllowedCommand.Download:
                await DownloadAsync(parsedArgs).ConfigureAwait(false);
                break;
            case ParsedArguments.AllowedCommand.List:
                await ListAsync(parsedArgs.ListOrDownloadAll).ConfigureAwait(false);
                break;
            case ParsedArguments.AllowedCommand.Dump:
                await DumpAsync().ConfigureAwait(false);
                break;
            case ParsedArguments.AllowedCommand.Help:
            case ParsedArguments.AllowedCommand.None:
            default:
                Usage();
                break;
        }
    }
}
