using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CliWrap;
using Leap.Cli.Platform;
using Leap.Cli.ProcessCompose.Yaml;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.ProcessCompose;

internal sealed class ProcessComposeManager : IProcessComposeManager
{
    private const string Version = "v0.80.0";
    private const string DownloadUrlFormat = "https://github.com/F1bonacc1/process-compose/releases/download/{0}/{1}";

    // Process Compose uses colors in its log output, which we don't want to show in the console
    private static readonly Regex AnsiEscapeSequenceRegex = new Regex("\x1B\\[[0-9;]*m", RegexOptions.Compiled);

    private readonly ICliWrap _cliWrap;
    private readonly IFileSystem _fileSystem;
    private readonly IPlatformHelper _platformHelper;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    private readonly string _exeDirPath;
    private readonly string _exeFilePath;
    private readonly string _configFilePath;
    private readonly string _logFilePath;
    private readonly string _downloadFilePath;

    public ProcessComposeManager(
        ICliWrap cliWrap,
        IFileSystem fileSystem,
        IPlatformHelper platformHelper,
        IHttpClientFactory httpClientFactory,
        ILogger<ProcessComposeManager> logger)
    {
        this._fileSystem = fileSystem;
        this._platformHelper = platformHelper;
        this._httpClientFactory = httpClientFactory;
        this._logger = logger;
        this._cliWrap = cliWrap;

        var exeFileName = this._platformHelper.CurrentOS == OSPlatform.Windows ? "process-compose.exe" : "process-compose";
        this._exeDirPath = Path.Combine(Constants.ProcessComposeDirectoryPath, "bin", Version);
        this._exeFilePath = Path.Combine(this._exeDirPath, exeFileName);

        this._configFilePath = Path.Combine(Constants.ProcessComposeDirectoryPath, "process-compose.yaml");
        this._logFilePath = Path.Combine(Constants.ProcessComposeDirectoryPath, "process-compose.log");

        var downloadFileName = this._platformHelper.CurrentOS == OSPlatform.Windows ? "process-compose.zip" : "process-compose.tar.gz";
        this._downloadFilePath = Path.Combine(this._exeDirPath, downloadFileName);

        this.Configuration = new ProcessComposeYaml();
    }

    public ProcessComposeYaml Configuration { get; }

    public async Task EnsureProcessComposeExecutableExistsAsync(CancellationToken cancellationToken)
    {
        if (!this._fileSystem.File.Exists(this._exeFilePath))
        {
            // TODO general error handling with better logging output in case something goes wrong
            this._fileSystem.Directory.CreateDirectory(this._exeDirPath);

            await this.DownloadProcessComposeArchiveAsync(cancellationToken);
            await this.UncompressProcessComposeArchiveAsync(cancellationToken);
            await this._platformHelper.MakeExecutableAsync(this._exeFilePath, cancellationToken);
        }
    }

    private async Task DownloadProcessComposeArchiveAsync(CancellationToken cancellationToken)
    {
        var platforms = new Dictionary<OSPlatform, string>
        {
            [OSPlatform.Windows] = "windows",
            [OSPlatform.Linux] = "linux",
            [OSPlatform.OSX] = "darwin",
        };

        var architectures = new Dictionary<Architecture, string>
        {
            [Architecture.X64] = "amd64",
            [Architecture.Arm64] = "arm64",
        };

        var extension = this._platformHelper.CurrentOS == OSPlatform.Windows ? "zip" : "tar.gz";

        var downloadFileName = $"process-compose_{platforms[this._platformHelper.CurrentOS]}_{architectures[this._platformHelper.ProcessArchitecture]}.{extension}";
        var downloadUrl = string.Format(DownloadUrlFormat, Version, downloadFileName);

        var httpClient = this._httpClientFactory.CreateClient();

        try
        {
            await using (var httpResponseStream = await httpClient.GetStreamAsync(downloadUrl, cancellationToken))
            await using (var destinationStream = this._fileSystem.File.Create(this._downloadFilePath))
            {
                await httpResponseStream.CopyToAsync(destinationStream, cancellationToken);
            }
        }
        catch (Exception)
        {
            this._fileSystem.File.Delete(this._downloadFilePath);
            throw;
        }
    }

    private async Task UncompressProcessComposeArchiveAsync(CancellationToken cancellationToken)
    {
        var unzipFlags = this._platformHelper.CurrentOS == OSPlatform.Windows ? "-xf" : "-xzf";

        var tar = new Command("tar")
            .WithValidation(CommandResultValidation.None)
            .WithWorkingDirectory(this._exeDirPath)
            .WithArguments([unzipFlags, this._downloadFilePath, "-C", this._exeDirPath]);

        // TODO handle exit code != 0 and executable not found
        try
        {
            await this._cliWrap.ExecuteBufferedAsync(tar, cancellationToken);
        }
        catch (Exception)
        {
            this._fileSystem.File.Delete(this._exeFilePath);
        }
        finally
        {
            this._fileSystem.File.Delete(this._downloadFilePath);
        }
    }

    public async Task WriteUpdatedProcessComposeFileAsync(CancellationToken cancellationToken)
    {
        await using var stream = this._fileSystem.File.Create(this._configFilePath);
        await ProcessComposeSerializer.SerializeAsync(stream, this.Configuration, cancellationToken);
    }

    public async Task StartProcessComposeAsync(CancellationToken cancellationToken)
    {
        var command = new Command(this._exeFilePath)
            .WithValidation(CommandResultValidation.None)
            .WithWorkingDirectory(Constants.ProcessComposeDirectoryPath)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(x => this._logger.LogDebug("{StandardOutput}", x)))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(x => this._logger.LogWarning("{StandardError}", x)))
            .WithArguments(new[]
            {
                "up",
                "--config", this._configFilePath,
                "--log-file", this._logFilePath,
                "--tui=false",
                "--port", "12346", // TODO Find another port
            });

        using var forcefulCts = new CancellationTokenSource();

        await using var cancellationRegistration = cancellationToken.Register(() =>
        {
            forcefulCts.CancelAfter(TimeSpan.FromSeconds(30));
        });

        var cliWrapExecuteTask = this._cliWrap.ExecuteAsync(command, forcefulCancellationToken: forcefulCts.Token, gracefulCancellationToken: cancellationToken);

        await cliWrapExecuteTask.ContinueWith(
            x => this.PostExecutionLoggingAsync(x, cancellationToken),
            cancellationToken,
            TaskContinuationOptions.RunContinuationsAsynchronously,
            TaskScheduler.Default);
    }

    private async Task PostExecutionLoggingAsync(Task<CommandResult> task, CancellationToken cancellationToken)
    {
        var result = await task;

        if (task.IsFaulted)
        {
            this._logger.LogError("An unexpected error occurred while orchestrating processes");
            await this.PrintProcessComposeLogAsync(cancellationToken);
        }
        else if (task.IsCompletedSuccessfully)
        {
            var logLevel = result.ExitCode == 0 ? LogLevel.Information : LogLevel.Error;
            this._logger.Log(logLevel, "Process orchestration exited with code {ExitCode}", result.ExitCode);
            await this.PrintProcessComposeLogAsync(cancellationToken);
        }
    }

    private async Task PrintProcessComposeLogAsync(CancellationToken cancellationToken)
    {
        try
        {
            using (var reader = new StreamReader(this._fileSystem.File.OpenRead(this._logFilePath)))
            {
                while (await reader.ReadLineAsync(cancellationToken) is { } line)
                {
                    this._logger.LogDebug(" - {LogLine}", AnsiEscapeSequenceRegex.Replace(line, string.Empty));
                }
            }
        }
        catch (IOException)
        {
        }
    }
}
