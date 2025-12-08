using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

using INuGetLogger = NuGet.Common.ILogger;
using IMsExtLogger = Microsoft.Extensions.Logging.ILogger<Leap.Cli.Platform.NuGetPackageDownloader>;

namespace Leap.Cli.Platform;

// Read this for more information about the NuGet client API:
// https://www.meziantou.net/exploring-the-nuget-client-libraries.htm
//
// This code was also written by searching how the NuGet client API were used on GitHub:
// https://github.com/search?q=%22new+SourceCacheContext%28%29%22&type=code
internal sealed class NuGetPackageDownloader : IDisposable, INuGetPackageDownloader
{
    private readonly INuGetLogger _nuGetLogger;
    private readonly IMsExtLogger _msExtLogger;
    private readonly SourceRepository _nugetOrgRepository;
    private readonly PackagePathResolver _packagePathResolver;
    private readonly SourceCacheContext _sourceCacheContext;
    private readonly PackageDownloadContext _packageDownloadContext;
    private readonly PackageExtractionContext _packageExtractionContext;
    private readonly string _globalPackagesFolder;

    public NuGetPackageDownloader(IMsExtLogger msExtLogger)
        : this(msExtLogger, Constants.NuGetPackagesDirectoryPath)
    {
    }

    internal NuGetPackageDownloader(IMsExtLogger msExtLogger, string packagesFolder)
    {
        this._nuGetLogger = NullLogger.Instance;
        this._msExtLogger = msExtLogger;

        this._nugetOrgRepository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        this._packagePathResolver = new PackagePathResolver(packagesFolder);

        this._sourceCacheContext = new SourceCacheContext();
        this._packageDownloadContext = new PackageDownloadContext(this._sourceCacheContext);

        var nugetUserSettings = Settings.LoadDefaultSettings(root: null);
        var clientPolicyContext = ClientPolicyContext.GetClientPolicy(nugetUserSettings, this._nuGetLogger);

        this._packageExtractionContext = new PackageExtractionContext(PackageSaveMode.Defaultv3, XmlDocFileSaveMode.Skip, clientPolicyContext, this._nuGetLogger);
        this._globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(nugetUserSettings);
    }

    public async Task<string> DownloadAndExtractPackageAsync(string id, string version, CancellationToken cancellationToken)
    {
        var packageIdentity = new PackageIdentity(id, new NuGetVersion(version));

        if (this._packagePathResolver.GetInstalledPath(packageIdentity) is { } alreadyExtractedPackagePath)
        {
            this._msExtLogger.LogTrace("NuGet package '{Id}' with version '{Version}' is already extracted to the directory '{Path}'.", id, version, alreadyExtractedPackagePath);
            return alreadyExtractedPackagePath;
        }

        var expectedExtractedPackagePath = this._packagePathResolver.GetInstallPath(packageIdentity);

        try
        {
            this._msExtLogger.LogDebug("Downloading and extracting NuGet package '{Id}' with version '{Version}' to the directory '{Path}'.", id, version, expectedExtractedPackagePath);

            var downloadResource = await this._nugetOrgRepository.GetResourceAsync<DownloadResource>(cancellationToken);

            using var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
                packageIdentity,
                this._packageDownloadContext,
                this._globalPackagesFolder,
                this._nuGetLogger,
                cancellationToken);

            if (downloadResult.Status == DownloadResourceResultStatus.NotFound)
            {
                throw new InvalidOperationException($"Could not download the NuGet package '{id}' with version '{version}' because it does not exist.");
            }

            await PackageExtractor.ExtractPackageAsync(
                downloadResult.PackageSource,
                downloadResult.PackageStream,
                this._packagePathResolver,
                this._packageExtractionContext,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Prevent leaving the partially extracted package on disk.
            Directory.Delete(expectedExtractedPackagePath, recursive: true);

            throw new InvalidOperationException($"Failed to download and extract NuGet package '{id}' with version '{version}' to the directory '{expectedExtractedPackagePath}'.", ex);
        }

        if (this._packagePathResolver.GetInstalledPath(packageIdentity) is { } extractedPackagePath)
        {
            return extractedPackagePath;
        }

        throw new InvalidOperationException($"Expected NuGet package '{id}' with version '{version}' to be extracted to '{expectedExtractedPackagePath}' but it was not found.");
    }

    public void Dispose()
    {
        this._sourceCacheContext.Dispose();
    }
}