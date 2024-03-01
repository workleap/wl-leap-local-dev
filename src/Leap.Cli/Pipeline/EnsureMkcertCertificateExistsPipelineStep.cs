using System.IO.Abstractions;
using System.Runtime.InteropServices;
using CliWrap;
using Leap.Cli.Extensions;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

// mkcert has over 40k+ stars on GitHub, is cross-platform, already used in ShareGate in various local setup scripts,
// and even recommended by Microsoft to setup Azurite with HTTPS: https://github.com/Azure/Azurite/blob/main/README.md#mkcert
// as well as Google: https://web.dev/articles/how-to-use-local-https
internal sealed class EnsureMkcertCertificateExistsPipelineStep : IPipelineStep
{
    // ".localhost" is a top-level domain (TLD) reserved by the Internet Engineering Task Force (IETF)
    // that is free to use localhost names as they would any other, without the risk of someone else owning it (like .com).
    // https://www.iana.org/assignments/special-use-domain-names/special-use-domain-names.xhtml
    // We didn't use ".local" because of the mDNS (Multicast DNS) protocol, which may cause issues accordind to this thread
    // https://www.reddit.com/r/sysadmin/comments/gdeggi/
    private static readonly string[] SupportedDomainNames =
    [
        "localhost", "127.0.0.1", "::1", // localhost
        "host.docker.internal", "host.containers.internal", // Docker and Podman
        "*.officevibe.localhost", "*.officevibe-dev.localhost", // Officevibe
        "*.sharegate.localhost", "*.sharegate-dev.localhost", // ShareGate
        "*.workleap.localhost", "*.workleap-dev.localhost" // Workleap
    ];

    private readonly IFeatureManager _featureManager;
    private readonly ICliWrap _cliWrap;
    private readonly IFileSystem _fileSystem;
    private readonly IPlatformHelper _platformHelper;
    private readonly ILogger _logger;

    public EnsureMkcertCertificateExistsPipelineStep(
        IFeatureManager featureManager,
        ICliWrap cliWrap,
        IFileSystem fileSystem,
        IPlatformHelper platformHelper,
        ILogger<EnsureMkcertCertificateExistsPipelineStep> logger)
    {
        this._featureManager = featureManager;
        this._cliWrap = cliWrap;
        this._fileSystem = fileSystem;
        this._platformHelper = platformHelper;
        this._logger = logger;
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (!this._featureManager.IsEnabled(FeatureIdentifiers.LeapPhase2))
        {
            this._logger.LogPipelineStepSkipped(nameof(EnsureMkcertCertificateExistsPipelineStep), FeatureIdentifiers.LeapPhase2);
            return;
        }

        var certAlreadyExists = this._fileSystem.File.Exists(Constants.LocalCertificateCrtFilePath) && this._fileSystem.File.Exists(Constants.LocalCertificateKeyFilePath);
        if (certAlreadyExists)
        {
            this._logger.LogInformation("Local development certificate already exists. Use it for HTTPS in your services:");
            this._logger.LogInformation(" - Certificate: {CertificateFilePath}", Constants.LocalCertificateCrtFilePath.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "~"));
            this._logger.LogInformation(" - Private key: {PrivateKeyFilePath}", Constants.LocalCertificateKeyFilePath.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "~"));

            return;
        }

        this._logger.LogTrace("Looking for mkcert in PATH...");
        var mkcertExePath = await this.FindMkcertExecutablePathInPathEnv(cancellationToken);
        if (mkcertExePath != null)
        {
            await this.CreateCertificateAsync(mkcertExePath, cancellationToken);
            return;
        }

        if (this._platformHelper.CurrentOS != OSPlatform.Windows)
        {
            throw new LeapException("mkcert is required to create a local certificate, but it couldn't be found. Please install it manually: https://github.com/FiloSottile/mkcert");
        }

        this._logger.LogTrace("Looking for mkcert in winget packages...");
        mkcertExePath = FindMkcertExecutablePathInWingetPackages();
        if (mkcertExePath != null)
        {
            await this.CreateCertificateAsync(mkcertExePath, cancellationToken);
            return;
        }

        this._logger.LogDebug("Installing mkcert with winget...");
        var mkcertInstallArgs = new[] { "install", "--id", "FiloSottile.mkcert", "--exact", "--disable-interactivity", "--version", "1.4.4" };
        var mkcertInstallCommand = new Command("winget").WithArguments(mkcertInstallArgs).WithValidation(CommandResultValidation.None);

        _ = await this._cliWrap.ExecuteBufferedAsync(mkcertInstallCommand, cancellationToken);

        this._logger.LogTrace("Looking for mkcert in winget packages after install...");
        mkcertExePath = FindMkcertExecutablePathInWingetPackages();
        if (mkcertExePath != null)
        {
            await this.CreateCertificateAsync(mkcertExePath, cancellationToken);
            return;
        }

        throw new LeapException("mkcert is required to create a local certificate, but it counldn't be found. Please install it manually: https://github.com/FiloSottile/mkcert");
    }

    private async Task<string?> FindMkcertExecutablePathInPathEnv(CancellationToken cancellationToken)
    {
        try
        {
            var mkcertNoopCommand = new Command("mkcert").WithArguments("-help").WithValidation(CommandResultValidation.ZeroExitCode);
            _ = await this._cliWrap.ExecuteBufferedAsync(mkcertNoopCommand, cancellationToken);

            return "mkcert";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindMkcertExecutablePathInWingetPackages()
    {
        // "winget --info" returns the following paths:
        var wingetPkgUserDirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages");
        var wingetPkgProgramFilesX64DirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinGet", "Packages");
        var wingetPkgProgramFilesX86DirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WinGet", "Packages");

        return EnumerateMkcertPackageDirPaths(wingetPkgUserDirPath)
            .Concat(EnumerateMkcertPackageDirPaths(wingetPkgProgramFilesX64DirPath))
            .Concat(EnumerateMkcertPackageDirPaths(wingetPkgProgramFilesX86DirPath))
            .SelectMany(EnumerateMkcertExecutablePaths)
            .FirstOrDefault();
    }

    private static IEnumerable<string> EnumerateMkcertPackageDirPaths(string wingetPackageDirPath)
    {
        try
        {
            return Directory.EnumerateDirectories(wingetPackageDirPath, "FiloSottile.mkcert*");
        }
        catch (IOException)
        {
            return Enumerable.Empty<string>();
        }
    }

    private static IEnumerable<string> EnumerateMkcertExecutablePaths(string mkcertPackageDirPath)
    {
        try
        {
            return Directory.EnumerateFiles(mkcertPackageDirPath, "mkcert*.exe");
        }
        catch (IOException)
        {
            return Enumerable.Empty<string>();
        }
    }

    private async Task CreateCertificateAsync(string mkcertExePath, CancellationToken cancellationToken)
    {
        if (this._platformHelper.CurrentOS == OSPlatform.Windows)
        {
            this._logger.LogWarning("Installing mkcert certificate authority, there might be a modal dialog asking for confirmation, please accept it");
        }

        var certAuthorityInstallCommand = new Command(mkcertExePath).WithArguments("-install").WithValidation(CommandResultValidation.None);
        var certAuthorityInstallResult = await this._cliWrap.ExecuteBufferedAsync(certAuthorityInstallCommand, cancellationToken);

        if (certAuthorityInstallResult.ExitCode != 0)
        {
            throw new LeapException("An error occured while installing the local certificate authority, please try again or run 'mkcert -install' manually, or follow the installation steps here: https://github.com/FiloSottile/mkcert");
        }

        this._logger.LogDebug("Creating the local development certificate...");

        string[] crtCreateArgs = ["-cert-file", Constants.LocalCertificateCrtFilePath, "-key-file", Constants.LocalCertificateKeyFilePath, .. SupportedDomainNames];
        var crtCreateCommand = new Command(mkcertExePath).WithArguments(crtCreateArgs).WithValidation(CommandResultValidation.None);
        var crtCreateResult = await this._cliWrap.ExecuteBufferedAsync(crtCreateCommand, cancellationToken);

        if (crtCreateResult.ExitCode != 0)
        {
            throw new LeapException($"An error occured while creating the local certificate, mkcert returned exit code {crtCreateResult.ExitCode}");
        }

        this._logger.LogInformation("Local development certificate created. Use it for HTTPS in your services:");
        this._logger.LogInformation(" - Certificate: {CertificateFilePath}", Constants.LocalCertificateCrtFilePath.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "~"));
        this._logger.LogInformation(" - Private key: {PrivateKeyFilePath}", Constants.LocalCertificateKeyFilePath.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "~"));
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
