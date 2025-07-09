using System.Text.Json.Serialization;

namespace EdgeDownloader.Downloader;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<ApiProductInfo>))]
[JsonSerializable(typeof(List<CdpRequestAttributes>))]
[JsonSerializable(typeof(List<ApiUpdateInfo>))]
[JsonSerializable(typeof(List<ApiDownloadInfo>))]
[JsonSerializable(typeof(ApiDownloadRequestInfo))]
[JsonSerializable(typeof(SortedSet<DumpInfo>))]
[JsonSerializable(typeof(PiecesHashInfo))]
internal partial class CommonJsonContext : JsonSerializerContext
{
}
