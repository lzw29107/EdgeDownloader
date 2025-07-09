namespace EdgeDownloader.Downloader;

public class TargetingAttributes
{
    string AppAp { get; set; } = string.Empty;
    string AppBrandCode { get; set; } = string.Empty;
    string AppCohort { get; set; } = string.Empty;
    string AppCohortHint { get; set; } = string.Empty;
    string AppCohortName { get; set; } = string.Empty;
    string AppLang { get; set; } = "en-us";
    int AppMajorVersion { get; set; }
    double AppRollOut { get; set; }
    string AppTTToken { get; set; } = string.Empty;
    string AppTargetVersionPrefix { get; set; } = string.Empty;
    Version AppVersion { get; set; } = Constants.InvalidVersion;
    string ExpETag { get; set; } = string.Empty;
    bool HW_AVX { get; set; } = true;
    int HW_DiskType { get; set; } = 2;
    int HW_LogicalCpus { get; set; } = 8;
    int HW_PhysicalRamGB { get; set; } = 8;
    bool HW_SSE { get; set; } = true;
    bool HW_SSE2 { get; set; } = true;
    bool HW_SSE3 { get; set; } = true;
    bool HW_SSE41 { get; set; } = true;
    bool HW_SSE42 { get; set; } = true;
    bool HW_SSSE3 { get; set; } = true;
    string InstallSource { get; set; } = string.Empty;
    bool IsInternalUser { get; set; }
    bool IsMachine { get; set; }
    bool IsWIP { get; set; }
    string OemProductManufacturer { get; set; } = string.Empty;
    string OemProductName { get; set; } = string.Empty;
    string OsArch { get; set; } = string.Empty;
    string OsPlatform { get; set; } = string.Empty;
    bool OsRegionDMA { get; set; }
    string OsRegionName { get; set; } = string.Empty;
    string OsRegionNation { get; set; } = string.Empty;
    Version OsVersion { get; set; } = Constants.InvalidVersion;
    int Priority { get; set; }
    string Updater { get; set; } = string.Empty;
    Version UpdaterVersion { get; set; } = Constants.InvalidVersion;
    string WIPBranch { get; set; } = string.Empty;
}
