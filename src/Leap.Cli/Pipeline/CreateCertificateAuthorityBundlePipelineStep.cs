using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Leap.Cli.Model;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

/// <summary>
/// Creates a certificate authority (CA) bundle for Docker containers from the host's root CA and personal certificates.
/// Mkcert's self-signed CA will also be part of this bundle, so containers can trust
/// local HTTPS endpoints protected with any certificate issued by mkcert.
/// </summary>
internal sealed class CreateCertificateAuthorityBundlePipelineStep(ILogger<CreateCertificateAuthorityBundlePipelineStep> logger)
    : IPipelineStep
{
    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        logger.LogDebug("Creating a certificate authority bundle for Docker containers at location '{Path}'", Constants.LeapCertificateAuthorityFilePath);

        try
        {
            // When mounting a file that does not exists, Docker creates an empty directory on the host. Make sure to delete it if this ever happens.
            Directory.Delete(Constants.LeapCertificateAuthorityFilePath, recursive: true);
        }
        catch
        {
            // Happens in the happy path when we already created the file and it is not a directory
        }

        try
        {
            var alreadyAddedCertificatesThumbprints = new HashSet<string>(StringComparer.Ordinal);

            // Overwriting the file at each run to keep it up-to-date with the host's root CA
            await using var bundleStream = new FileStream(Constants.LeapCertificateAuthorityFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var bundleWriter = new StreamWriter(bundleStream);
            bundleWriter.NewLine = "\n";

            await PopulateCertificateAuthorityBundleFromHostStore(StoreName.Root, StoreLocation.LocalMachine); // mkcert's CA was found here on Ubuntu
            await PopulateCertificateAuthorityBundleFromHostStore(StoreName.Root, StoreLocation.CurrentUser); // mkcert's CA was found here on Windows
            await PopulateCertificateAuthorityBundleFromHostStore(StoreName.My, StoreLocation.LocalMachine); // mkcert's CA was found here on macOS
            await PopulateCertificateAuthorityBundleFromHostStore(StoreName.My, StoreLocation.CurrentUser); // include other personal certificates

            async Task PopulateCertificateAuthorityBundleFromHostStore(StoreName storeName, StoreLocation storeLocation)
            {
                using var store = new X509Store(storeName, storeLocation);

                try
                {
                    store.Open(OpenFlags.ReadOnly);
                }
                catch
                {
                    // Not all stores are accessible on all platforms
                    return;
                }

                foreach (var cert in store.Certificates)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!alreadyAddedCertificatesThumbprints.Add(cert.Thumbprint))
                    {
                        continue;
                    }

                    try
                    {
                        // A linux certificate authority bundle is a simple text file where all the certificates
                        // are concatenated together in PEM format. See: https://unix.stackexchange.com/a/515195/543055
                        // No cancellation token is passed in order not to corrupt the resulting file
                        await bundleWriter.WriteLineAsync(cert.ExportCertificatePem().AsMemory(), CancellationToken.None);
                    }
                    catch (CryptographicException)
                    {
                        // Ignore corrupted certificates
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Application is shutting down
        }
        catch (Exception ex)
        {
            throw new LeapException("Failed to create the certificate authority bundle for Docker containers", ex);
        }
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}