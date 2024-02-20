using System.IO.Abstractions;
using System.Runtime.InteropServices;
using Leap.Cli.Platform;

namespace Leap.Cli.ProcessCompose;

internal sealed class PrismManager : IPrismManager
{
    private const string Version = "v5.5.3";
    private const string DownloadUrlFormat = "https://github.com/stoplightio/prism/releases/download/{0}/{1}";

    private static readonly Dictionary<OSPlatform, string> ExecutableNames = new()
    {
        [OSPlatform.Windows] = "prism-cli-win.exe",
        [OSPlatform.Linux] = "prism-cli-linux",
        [OSPlatform.OSX] = "prism-cli-macos",
    };

    private readonly ICliWrap _cliWrap;
    private readonly IFileSystem _fileSystem;
    private readonly IPlatformHelper _platformHelper;
    private readonly IHttpClientFactory _httpClientFactory;

    private readonly string _exeDirPath;
    private readonly string _exeFilePath;

    public PrismManager(
        ICliWrap cliWrap,
        IFileSystem fileSystem,
        IPlatformHelper platformHelper,
        IHttpClientFactory httpClientFactory)
    {
        this._cliWrap = cliWrap;
        this._fileSystem = fileSystem;
        this._platformHelper = platformHelper;
        this._httpClientFactory = httpClientFactory;

        var exeFileName = ExecutableNames[this._platformHelper.CurrentOS];
        this._exeDirPath = Path.Combine(Constants.PrismDirectoryPath, "bin", Version);
        this._exeFilePath = Path.Combine(this._exeDirPath, exeFileName);
    }

    public string PrismExecutablePath => this._exeFilePath;

    public async Task EnsurePrismExecutableExistsAsync(CancellationToken cancellationToken)
    {
        if (!this._fileSystem.File.Exists(this._exeFilePath))
        {
            // TODO general error handling with better logging output in case something goes wrong
            this._fileSystem.Directory.CreateDirectory(this._exeDirPath);

            await this.DownloadPrismExecutableAsync(cancellationToken);
            await this._platformHelper.MakeExecutableAsync(this._exeFilePath, cancellationToken);
        }
    }

    private async Task DownloadPrismExecutableAsync(CancellationToken cancellationToken)
    {
        var downloadUrl = string.Format(DownloadUrlFormat, Version, ExecutableNames[this._platformHelper.CurrentOS]);

        var httpClient = this._httpClientFactory.CreateClient();

        try
        {
            await using (var httpResponseStream = await httpClient.GetStreamAsync(downloadUrl, cancellationToken))
            await using (var destinationStream = this._fileSystem.File.Create(this._exeFilePath))
            {
                await httpResponseStream.CopyToAsync(destinationStream, cancellationToken);
            }
        }
        catch (Exception)
        {
            this._fileSystem.File.Delete(this._exeFilePath);
            throw;
        }
    }
}
