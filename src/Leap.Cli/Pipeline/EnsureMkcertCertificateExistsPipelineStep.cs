using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using CliWrap;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

// mkcert has over 40k+ stars on GitHub, is cross-platform, already used in ShareGate in various local setup scripts,
// and even recommended by Microsoft to setup Azurite with HTTPS: https://github.com/Azure/Azurite/blob/main/README.md#mkcert
// as well as Google: https://web.dev/articles/how-to-use-local-https
internal sealed class EnsureMkcertCertificateExistsPipelineStep : IPipelineStep
{
    private readonly ICliWrap _cliWrap;
    private readonly IFileSystem _fileSystem;
    private readonly IPlatformHelper _platformHelper;
    private readonly ILogger _logger;

    public EnsureMkcertCertificateExistsPipelineStep(
        ICliWrap cliWrap,
        IFileSystem fileSystem,
        IPlatformHelper platformHelper,
        ILogger<EnsureMkcertCertificateExistsPipelineStep> logger)
    {
        this._cliWrap = cliWrap;
        this._fileSystem = fileSystem;
        this._platformHelper = platformHelper;
        this._logger = logger;
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        this.DeleteExistingCertificateWhenUpdateIsRequired();

        var certificateAlreadyExists = this._fileSystem.File.Exists(Constants.LocalCertificateCrtFilePath) && this._fileSystem.File.Exists(Constants.LocalCertificateKeyFilePath);
        if (certificateAlreadyExists)
        {
            var homeDirectoryShorthand = this._platformHelper.CurrentOS == OSPlatform.Windows ? "%USERPROFILE%" : "~";

            this._logger.LogInformation("Local development certificate already exists. Use it for HTTPS in your services:");
            this._logger.LogInformation(" - Certificate: {CertificateFilePath}", Constants.LocalCertificateCrtFilePath.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), homeDirectoryShorthand));
            this._logger.LogInformation(" - Private key: {PrivateKeyFilePath}", Constants.LocalCertificateKeyFilePath.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), homeDirectoryShorthand));

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

    private void DeleteExistingCertificateWhenUpdateIsRequired()
    {
        var existingCertificate = LoadExistingCertificate();
        if (existingCertificate == null)
        {
            return;
        }

        List<string> notSupportedWildcardDomainNames = [];

        foreach (var wildcardDomainName in Constants.SupportedWildcardLocalhostDomainNames)
        {
            var exampleDomainName = ConvertWildcardDomainToConcreteExample(wildcardDomainName);

            if (!existingCertificate.MatchesHostname(exampleDomainName))
            {
                notSupportedWildcardDomainNames.Add(wildcardDomainName);
            }
        }

        if (notSupportedWildcardDomainNames.Count > 0)
        {
            this._logger.LogDebug("The existing certificate must be recreated because it doesn't support the following domain names: {NotSupportedWildcardDomainNames}", string.Join(", ", notSupportedWildcardDomainNames));

            try
            {
                File.Delete(Constants.LocalCertificateCrtFilePath);
                File.Delete(Constants.LocalCertificateKeyFilePath);
            }
            catch (IOException ex)
            {
                throw new LeapException($"An error occured while deleting the existing local development certificate '{Constants.LocalCertificateCrtFilePath}' and its key '{Constants.LocalCertificateKeyFilePath}' in order to recreate it to support more domains: {ex.Message.TrimEnd('.')}. Please try to delete the files manually.", ex);
            }
        }
    }

    private static X509Certificate2? LoadExistingCertificate()
    {
        try
        {
            return X509Certificate2.CreateFromPemFile(Constants.LocalCertificateCrtFilePath, Constants.LocalCertificateKeyFilePath);
        }
        catch (FileNotFoundException)
        {
            // Expected when the certificate hasn't been created yet
        }
        catch (Exception ex)
        {
            throw new LeapException($"An error occured while loading the local development certificate '{Constants.LocalCertificateCrtFilePath}' and its key '{Constants.LocalCertificateKeyFilePath}': {ex.Message}", ex);
        }

        return null;
    }

    private static string ConvertWildcardDomainToConcreteExample(string domain)
    {
        const string wildcard = "*";
        return domain.Replace(wildcard, "example");
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

        string[] crtCreateArgs = ["-cert-file", Constants.LocalCertificateCrtFilePath, "-key-file", Constants.LocalCertificateKeyFilePath, .. Constants.MkcertSupportedDomainNames];
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
