using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using CliWrap;
using Leap.Cli.Commands;
using Leap.Cli.Pipeline;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Platform;

// mkcert has over 40k+ stars on GitHub, is cross-platform, already used in ShareGate in various local setup scripts,
// and even recommended by Microsoft to setup Azurite with HTTPS: https://github.com/Azure/Azurite/blob/v3.33.0/README.md#mkcert
// as well as Google: https://web.dev/articles/how-to-use-local-https
internal sealed class MkcertCertificateManager(ICliWrap cliWrap, IFileSystem fileSystem, IPlatformHelper platformHelper, ILogger<MkcertCertificateManager> logger)
{
    public async Task EnsureCertificateIsInstalledInLeapDirectoryAsync(CancellationToken cancellationToken)
    {
        this.DeleteExistingCertificateWhenUpdateIsRequired();

        var certificateAlreadyExists = fileSystem.File.Exists(Constants.LocalCertificateCrtFilePath) && fileSystem.File.Exists(Constants.LocalCertificateKeyFilePath);
        if (certificateAlreadyExists)
        {
            var homeDirectoryShorthand = platformHelper.CurrentOS == OSPlatform.Windows ? "%USERPROFILE%" : "~";

            logger.LogInformation("Local development certificate already exists. Use it for HTTPS in your services:");
            logger.LogInformation(" - Certificate: {CertificateFilePath}", Constants.LocalCertificateCrtFilePath.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), homeDirectoryShorthand));
            logger.LogInformation(" - Private key: {PrivateKeyFilePath}", Constants.LocalCertificateKeyFilePath.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), homeDirectoryShorthand));

            return;
        }

        logger.LogTrace("Looking for mkcert in PATH...");
        var mkcertExePath = await this.FindMkcertExecutablePathInPathEnv(cancellationToken);
        if (mkcertExePath != null)
        {
            await this.CreateCertificateAsync(mkcertExePath, cancellationToken);
            return;
        }

        if (platformHelper.CurrentOS != OSPlatform.Windows)
        {
            throw new LeapException("mkcert is required to create a local certificate, but it couldn't be found. Please install it manually: https://github.com/FiloSottile/mkcert");
        }

        logger.LogTrace("Looking for mkcert in winget packages...");
        mkcertExePath = FindMkcertExecutablePathInWingetPackages();
        if (mkcertExePath != null)
        {
            await this.CreateCertificateAsync(mkcertExePath, cancellationToken);
            return;
        }

        logger.LogDebug("Installing mkcert with winget...");
        var mkcertInstallArgs = new[] { "install", "--id", "FiloSottile.mkcert", "--exact", "--disable-interactivity", "--version", "1.4.4", "--source", "winget" };
        var mkcertInstallCommand = new Command("winget").WithArguments(mkcertInstallArgs).WithValidation(CommandResultValidation.None);

        _ = await cliWrap.ExecuteBufferedAsync(mkcertInstallCommand, cancellationToken);

        logger.LogTrace("Looking for mkcert in winget packages after install...");
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

        var isExpiringSoon = IsCertificateExpiringSoon(existingCertificate);

        if (notSupportedWildcardDomainNames.Count > 0 || isExpiringSoon)
        {
            if (notSupportedWildcardDomainNames.Count > 0)
            {
                logger.LogDebug("The existing certificate must be recreated because it doesn't support the following domain names: {NotSupportedWildcardDomainNames}", string.Join(", ", notSupportedWildcardDomainNames));
            }

            if (isExpiringSoon)
            {
                logger.LogDebug("The existing certificate must be recreated because it is expiring soon (expires on {ExpirationDate})", existingCertificate.NotAfter);
            }

            try
            {
                File.Delete(Constants.LocalCertificateCrtFilePath);
                File.Delete(Constants.LocalCertificateKeyFilePath);
            }
            catch (IOException ex)
            {
                throw new LeapException($"An error occurred while deleting the existing local development certificate '{Constants.LocalCertificateCrtFilePath}' and its key '{Constants.LocalCertificateKeyFilePath}' in order to recreate it: {ex.Message.TrimEnd('.')}. Please try to delete the files manually.", ex);
            }
        }
    }

    internal static X509Certificate2? LoadExistingCertificate()
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
            throw new LeapException($"An error occurred while loading the local development certificate '{Constants.LocalCertificateCrtFilePath}' and its key '{Constants.LocalCertificateKeyFilePath}': {ex.Message}", ex);
        }

        return null;
    }

    private static string ConvertWildcardDomainToConcreteExample(string domain)
    {
        const string wildcard = "*";
        return domain.Replace(wildcard, "example");
    }

    private const int ExpirationWarningThresholdDays = 30;

    internal static bool IsCertificateExpiringSoon(X509Certificate2 certificate)
    {
        // Check if the certificate is expiring within the threshold
        var expirationThreshold = DateTime.UtcNow.AddDays(ExpirationWarningThresholdDays);
        return certificate.NotAfter.ToUniversalTime() <= expirationThreshold;
    }

    private async Task<string?> FindMkcertExecutablePathInPathEnv(CancellationToken cancellationToken)
    {
        try
        {
            var mkcertNoopCommand = new Command("mkcert").WithArguments("-help").WithValidation(CommandResultValidation.ZeroExitCode);
            _ = await cliWrap.ExecuteBufferedAsync(mkcertNoopCommand, cancellationToken);

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
            return [];
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
            return [];
        }
    }

    private async Task CreateCertificateAsync(string mkcertExePath, CancellationToken cancellationToken)
    {
        if (platformHelper.CurrentOS == OSPlatform.Windows)
        {
            logger.LogWarning("Installing mkcert certificate authority, there might be a modal dialog asking for confirmation, please accept it");
        }

        var certAuthorityInstallCommand = new Command(mkcertExePath).WithArguments("-install").WithValidation(CommandResultValidation.None);
        var certAuthorityInstallResult = await cliWrap.ExecuteBufferedAsync(certAuthorityInstallCommand, cancellationToken);

        if (certAuthorityInstallResult.ExitCode != 0)
        {
            throw new LeapException("An error occurred while installing the local certificate authority, please try again or run 'mkcert -install' manually, or follow the installation steps here: https://github.com/FiloSottile/mkcert");
        }

        logger.LogDebug("Creating the local development certificate...");

        string[] crtCreateArgs = ["-cert-file", Constants.LocalCertificateCrtFilePath, "-key-file", Constants.LocalCertificateKeyFilePath, .. Constants.MkcertSupportedDomainNames];
        var crtCreateCommand = new Command(mkcertExePath).WithArguments(crtCreateArgs).WithValidation(CommandResultValidation.None);
        var crtCreateResult = await cliWrap.ExecuteBufferedAsync(crtCreateCommand, cancellationToken);

        if (crtCreateResult.ExitCode != 0)
        {
            throw new LeapException($"An error occurred while creating the local certificate, mkcert returned exit code {crtCreateResult.ExitCode}");
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            // Docker containers started by Leap may run as another user (root-less). We need to make the certificate files readable by everyone, so they can be loaded from the containers.
            TryUpdateCertificateFilePermissions(Constants.LocalCertificateCrtFilePath);
            TryUpdateCertificateFilePermissions(Constants.LocalCertificateKeyFilePath);
            TryUpdateCertificateFilePermissions(Constants.LeapCertificateAuthorityFilePath);

            static void TryUpdateCertificateFilePermissions(string path)
            {
                try
                {
                    File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
                }
                catch
                {
                    // Ignore any errors
                }
            }
        }

        logger.LogInformation("Local development certificate created. Use it for HTTPS in your services:");
        logger.LogInformation(" - Certificate: {CertificateFilePath}", Constants.LocalCertificateCrtFilePath.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "~"));
        logger.LogInformation(" - Private key: {PrivateKeyFilePath}", Constants.LocalCertificateKeyFilePath.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "~"));
    }

    // https://github.com/FiloSottile/mkcert/blob/v1.4.4/cert.go#L330
    private const string MkcertCertificateIssuerName = "mkcert development CA";

    public async Task EnsureCertificateAuthorityIsInstalledInComputerRootStoreAsync(CancellationToken cancellationToken)
    {
        if (platformHelper.CurrentOS != OSPlatform.Windows)
        {
            // We only want to do this on Windows for Officevibe's IIS elevated process to trust the certificate authority
            // which is by default installed in the current user's root store
            return;
        }

        using (var machineRootStore = new X509Store(StoreName.Root, StoreLocation.LocalMachine))
        {
            machineRootStore.Open(OpenFlags.ReadOnly);

            var mkcertCerts = machineRootStore.Certificates.Find(X509FindType.FindByIssuerName, MkcertCertificateIssuerName, validOnly: true);
            if (mkcertCerts.Count > 0)
            {
                logger.LogTrace("Mkcert certificate authority is already installed in the computer's root store");
                return;
            }
        }

        if (platformHelper.IsCurrentProcessElevated)
        {
            this.AddAuthorityToLocalComputerRootStore();
        }
        else
        {
            logger.LogWarning("Please accept to elevate the process to install the certificate authority in the computer's root store");
            await platformHelper.StartLeapElevatedAsync([AddCertificateAuthorityToComputerRootStoreCommand.CommandName], cancellationToken);
        }
    }

    [SuppressMessage("Security", "CA5380:Do not add certificates to root store", Justification = "This is only scoped to mkcert's local development CA")]
    private void AddAuthorityToLocalComputerRootStore()
    {
        logger.LogDebug("Adding mkcert certificate authority to the computer's root store...");
        using var userRootStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
        userRootStore.Open(OpenFlags.ReadOnly);

        using var machineRootStore = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        machineRootStore.Open(OpenFlags.ReadWrite);

        var mkcertCerts = userRootStore.Certificates.Find(X509FindType.FindByIssuerName, MkcertCertificateIssuerName, validOnly: true);

        foreach (var mkcertCert in mkcertCerts)
        {
            machineRootStore.Add(mkcertCert);
        }
    }
}