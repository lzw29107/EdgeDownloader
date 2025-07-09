global using StandaloneProductType = (EdgeDownloader.Downloader.EdgeProductType EdgeType, EdgeDownloader.FwLinkId FwLinkId);

namespace EdgeDownloader;

public enum SupportedChannel
{
    Stable,
    Beta,
    Dev,
    Canary
}

public enum SupportedOS
{
    Android,
    iOS,
    Linux,
    MacOS,
    WCOS,
    Windows,
    Win7And8
}

public enum SupportedArch
{
    X86,
    X64,
    Arm,
    Arm64,
    Universal // Universal binaries for macOS
}

public enum SupportedProduct
{
    Edge,
    EdgeEnterprise,
    EdgeWebView2,
    EdgeUpdate
}

public enum FileExtType
{
    Exe,
    Msi,
    Deb,
    Rpm,
    Pkg,
    Dmg,
    Apk,
    Msix
}

public enum RequestAction
{
    BatchUpdates,
    GenerateDownloadInfo
}

public enum FwLinkId
{
    EdgeCanaryUniversalPkg = 2069147,
    EdgeStableUniversalPkg = 2069148,
    EdgeDevUniversalPkg = 2069340,
    EdgeStableArm64ApkCatapult = 2069341,
    EdgeBetaUniversalPkg = 2069439,
    EdgeEnterpriseDevX64Msi = 2093291,
    EdgeDevUniversalDmg = 2093292,
    EdgeCanaryUniversalDmg = 2093293,
    EdgeBetaUniversalDmg = 2093294,
    EdgeEnterpriseBetaX64Msi = 2093376,
    EdgeEnterpriseBetaX86Msi = 2093377,
    EdgeEnterpriseDevX86Msi = 2093436,
    EdgeEnterpriseStableX64Msi = 2093437,
    EdgeStableArmApkTencent = 2093438,
    EdgeStableUniversalPkgNew = 2093504,
    EdgeEnterpriseStableX86Msi = 2093505,
    EdgeStableArm64ApkHuawei = 2099521,
    EdgeStableArm64ApkAmazon = 2099615,
    EdgeWebView2StableArm64Exe = 2099616,
    EdgeWebView2StableX86Exe = 2099617,
    EdgeBetaUniversalPkgNew = 2099618,
    EdgeDevUniversalPkgNew = 2099619,
    EdgeCanaryArm64Msix = 2107436,
    EdgeDevArm64Msix = 2107437,
    EdgeBetaArm64Msix = 2107438,
    EdgeWebView2CanaryArm64Msix = 2107527,
    EdgeStableArm64ApkSamsung = 2107528,
    EdgeStableArm64ApkXiaomi = 2107647,
    EdgeWebView2DevArm64Msix = 2124311,
    EdgeStableArm64ApkOpera = 2124601,
    EdgeDevX64Deb = 2124602,
    EdgeCanaryUniversalDmgNew = 2124603,
    EdgeWebView2BetaArm64Msix = 2124700,
    EdgeWebView2StableX64Exe = 2124701,
    EdgeDevX64Rpm = 2124702,
    EdgeStableArm64ApkLenovo = 2148793,
    EdgeStableArm64ApkBrowser = 2148794,
    EdgeStableArm64ApkTencent = 2148870,
    EdgeStableArm64ApkOppo = 2148871,
    EdgeStableArm64Apk360 = 2148872,
    EdgeStableArm64ApkGetjar = 2148873,
    EdgeStableX64Deb = 2149051,
    EdgeStableX64Rpm = 2149137,
    EdgeBetaX64Rpm = 2149138,
    EdgeBetaX64Deb = 2149139,
    EdgeStableArm64ApkApkpure = 2184663,
    EdgeStableArm64ApkMeizu = 2184664,
    EdgeStableArm64ApkBaidu = 2184665,
    EdgeStableArmApkBrowser = 2184828,
    EdgeStableArm64ApkCoolapk = 2184829,
    EdgeStableArm64ApkVivo = 2184830,
    EdgeStableArm64ApkAlibaba = 2188714,
    EdgeStableArmApkVivo = 2188715,
    EdgeCanaryArmApk = 2188716,
    EdgeDevArmApk = 2188717,
    EdgeBetaArm64Apk = 2188718,
    EdgeBetaArmApk = 2188807,
    EdgeDevArm64Apk = 2188808,
    EdgeCanaryArm64Apk = 2188809,
    EdgeStableArm64ApkHuaweiOverseas = 2192088,
    EdgeStableUniversalDmg = 2192091,
    EdgeStableArm64ApkHonor = 2192092,
    EdgeStableArm64ApkSoftonic = 2192093,
    EdgeStableArm64ApkAptoide = 2192094,
    EdgeBetaUniversalDmgCN = 2192180,
    EdgeStableArm64ApkCafebazarr = 2192181,
    EdgeStableArm64ApkOnestore = 2192182,
    EdgeStableArm64ApkTranssion = 2192183,
    EdgeStableArm64ApkUptodown = 2192184,
    EdgeStableArm64Apk9Apps = 2192185,
    EdgeStableArm64ApkPaidads = 2192536,
    EdgeWebView2StableArm64Msix = 2270343,
    EdgeDevUniversalDmgCN = 2281007,
    EdgeCanaryUniversalDmgCN = 2294775,
    EdgeStableUniversalDmgCN = 2325016
}