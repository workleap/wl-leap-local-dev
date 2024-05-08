namespace Leap.Cli.Platform;

internal interface INuGetPackageDownloader
{
    Task<string> DownloadAndExtractPackageAsync(string id, string version, CancellationToken cancellationToken);
}