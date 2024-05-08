using Leap.Cli.Platform;
using Microsoft.Extensions.Logging.Abstractions;

namespace Leap.Cli.Tests.Platform;

public sealed class NuGetPackageDownloaderTests : IDisposable
{
    private readonly DirectoryInfo _temporaryPackagesDir;
    private readonly NuGetPackageDownloader _nuGetPackageDownloader;

    public NuGetPackageDownloaderTests()
    {
        this._temporaryPackagesDir = Directory.CreateTempSubdirectory();
        this._nuGetPackageDownloader = new NuGetPackageDownloader(NullLogger<NuGetPackageDownloader>.Instance, this._temporaryPackagesDir.FullName);
    }

    [Fact]
    public async Task DownloadAndExtractPackageAsync_Works()
    {
        // This package was chosen because we use Aspire, but it could be any package.
        const string packageId = "Aspire.Dashboard.Sdk.win-x64";
        const string packageVersion = "8.0.0-preview.6.24214.1";

        await this._nuGetPackageDownloader.DownloadAndExtractPackageAsync(packageId, packageVersion, CancellationToken.None);

        var expectedPackageDir = Path.Combine(this._temporaryPackagesDir.FullName, "Aspire.Dashboard.Sdk.win-x64.8.0.0-preview.6.24214.1");
        Assert.True(Directory.Exists(expectedPackageDir), userMessage: $"The package {packageId} should have been downloaded and extracted.");
        Assert.NotEmpty(Directory.EnumerateFileSystemEntries(expectedPackageDir));
    }

    public void Dispose()
    {
        this._nuGetPackageDownloader.Dispose();
        this._temporaryPackagesDir.Delete(recursive: true);
    }
}