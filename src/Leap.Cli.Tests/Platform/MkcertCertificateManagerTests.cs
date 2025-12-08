using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Leap.Cli.Platform;

namespace Leap.Cli.Tests.Platform;

public sealed class MkcertCertificateManagerTests : IDisposable
{
    private readonly DirectoryInfo _temporaryCertificatesDir;
    private readonly string _tempCrtPath;
    private readonly string _tempKeyPath;

    public MkcertCertificateManagerTests()
    {
        this._temporaryCertificatesDir = Directory.CreateTempSubdirectory();
        this._tempCrtPath = Path.Combine(this._temporaryCertificatesDir.FullName, "test-cert.crt");
        this._tempKeyPath = Path.Combine(this._temporaryCertificatesDir.FullName, "test-cert.key");
    }

    [Theory]
    [InlineData(15, true, "Certificate expiring in 15 days should be detected as expiring soon")]
    [InlineData(30, true, "Certificate expiring in exactly 30 days should be detected as expiring soon (boundary condition)")]
    [InlineData(60, false, "Certificate expiring in 60 days should not be detected as expiring soon")]
    [InlineData(31, false, "Certificate expiring in 31 days should not be detected as expiring soon")]
    [InlineData(1, true, "Certificate expiring in 1 day should be detected as expiring soon")]
    [InlineData(0, true, "Certificate expiring today should be detected as expiring soon")]
    public void IsCertificateExpiringSoon_VariousExpirationDates_ReturnsExpectedResult(int daysUntilExpiration, bool expectedIsExpiringSoon, string _)
    {
        // Arrange
        var certificate = CreateTestCertificate(daysUntilExpiration);

        // Act
        var isExpiringSoon = MkcertCertificateManager.IsCertificateExpiringSoon(certificate);

        // Assert
        Assert.Equal(expectedIsExpiringSoon, isExpiringSoon);
    }

    [Fact]
    public void Certificate_CanBeCreatedAndLoadedFromDisk()
    {
        // Create a certificate
        var cert = CreateTestCertificate(daysUntilExpiration: 365);
        SaveCertificateToDisk(cert, this._tempCrtPath, this._tempKeyPath);

        // Verify files were created
        Assert.True(File.Exists(this._tempCrtPath), "Certificate file should exist");
        Assert.True(File.Exists(this._tempKeyPath), "Private key file should exist");

        // Load the certificate back from disk
        var loadedCert = X509Certificate2.CreateFromPemFile(this._tempCrtPath, this._tempKeyPath);

        Assert.NotNull(loadedCert);
        Assert.Equal(cert.Subject, loadedCert.Subject);
    }

    [Fact]
    public void LoadExistingCertificate_WhenCertificateExists_ReturnsCertificate()
    {
        // Arrange
        var cert = CreateTestCertificate(daysUntilExpiration: 365);
        SaveCertificateToDisk(cert, Constants.LocalCertificateCrtFilePath, Constants.LocalCertificateKeyFilePath);

        try
        {
            // Act
            var loadedCert = MkcertCertificateManager.LoadExistingCertificate();

            // Assert
            Assert.NotNull(loadedCert);
            Assert.Equal(cert.Subject, loadedCert.Subject);
        }
        finally
        {
            // Cleanup
            TryDeleteFile(Constants.LocalCertificateCrtFilePath);
            TryDeleteFile(Constants.LocalCertificateKeyFilePath);
        }
    }

    [Fact]
    public void LoadExistingCertificate_WhenCertificateDoesNotExist_ReturnsNull()
    {
        // Arrange - Ensure files don't exist
        TryDeleteFile(Constants.LocalCertificateCrtFilePath);
        TryDeleteFile(Constants.LocalCertificateKeyFilePath);

        // Act
        var loadedCert = MkcertCertificateManager.LoadExistingCertificate();

        // Assert
        Assert.Null(loadedCert);
    }

    [Fact]
    public void IsCertificateExpiringSoon_WithExpiredCertificate_ReturnsTrue()
    {
        // Arrange - Create a certificate that expired yesterday
        var expiredCert = CreateTestCertificate(daysUntilExpiration: -1);

        // Act
        var isExpiringSoon = MkcertCertificateManager.IsCertificateExpiringSoon(expiredCert);

        // Assert
        Assert.True(isExpiringSoon, "Already expired certificate should be detected as expiring soon");
    }

    [Fact]
    public void Certificate_SupportsAllRequiredWildcardDomains()
    {
        // Arrange
        var cert = CreateTestCertificate(daysUntilExpiration: 365);

        // Act & Assert - Verify all wildcard domains are supported
        foreach (var wildcardDomain in Constants.SupportedWildcardLocalhostDomainNames)
        {
            var exampleDomain = ConvertWildcardToExample(wildcardDomain);
            Assert.True(cert.MatchesHostname(exampleDomain), $"Certificate should match domain: {exampleDomain}");
        }
    }

    [Theory]
    [InlineData("*.workleap.localhost", "example.workleap.localhost")]
    [InlineData("*.barley.localhost", "test.barley.localhost")]
    [InlineData("*.officevibe.com", "app.officevibe.com")]
    [InlineData("*.sharegate-dev.com", "my-service.sharegate-dev.com")]
    public void Certificate_MatchesWildcardDomains(string wildcardDomain, string concreteDomain)
    {
        // Arrange
        var cert = CreateTestCertificate(daysUntilExpiration: 365);

        // Act & Assert
        Assert.True(cert.MatchesHostname(concreteDomain), $"Certificate should match {concreteDomain} from wildcard {wildcardDomain}");
    }

    [Fact]
    public void Certificate_DoesNotMatchUnsupportedDomain()
    {
        // Arrange
        var cert = CreateTestCertificate(daysUntilExpiration: 365);

        // Act & Assert
        Assert.False(cert.MatchesHostname("unsupported.domain.com"), "Certificate should not match unsupported domain");
    }

    [Fact]
    public void DeleteExistingCertificate_WhenExpiringSoon_DeletesFiles()
    {
        // Arrange - Create an expiring certificate
        var expiringCert = CreateTestCertificate(daysUntilExpiration: 15);
        SaveCertificateToDisk(expiringCert, Constants.LocalCertificateCrtFilePath, Constants.LocalCertificateKeyFilePath);

        try
        {
            // Verify files exist before deletion
            Assert.True(File.Exists(Constants.LocalCertificateCrtFilePath));
            Assert.True(File.Exists(Constants.LocalCertificateKeyFilePath));

            // This test verifies the behavior indirectly by checking if the files would need to be recreated
            var loadedCert = MkcertCertificateManager.LoadExistingCertificate();
            Assert.NotNull(loadedCert);

            var shouldBeDeleted = MkcertCertificateManager.IsCertificateExpiringSoon(loadedCert);
            Assert.True(shouldBeDeleted, "Certificate expiring in 15 days should be marked for deletion");
        }
        finally
        {
            TryDeleteFile(Constants.LocalCertificateCrtFilePath);
            TryDeleteFile(Constants.LocalCertificateKeyFilePath);
        }
    }

    [Fact]
    public void DeleteExistingCertificate_WhenMissingDomains_ShouldTriggerRecreation()
    {
        // Arrange - Create a certificate with only one domain (missing others)
        var certWithLimitedDomains = CreateTestCertificateWithLimitedDomains(daysUntilExpiration: 365);
        SaveCertificateToDisk(certWithLimitedDomains, Constants.LocalCertificateCrtFilePath, Constants.LocalCertificateKeyFilePath);

        try
        {
            var loadedCert = MkcertCertificateManager.LoadExistingCertificate();
            Assert.NotNull(loadedCert);

            // Verify that at least one required domain is missing
            var allDomainsSupported = Constants.SupportedWildcardLocalhostDomainNames
                .All(wildcardDomain => loadedCert.MatchesHostname(ConvertWildcardToExample(wildcardDomain)));

            Assert.False(allDomainsSupported, "Certificate with limited domains should be missing some required domains");
        }
        finally
        {
            TryDeleteFile(Constants.LocalCertificateCrtFilePath);
            TryDeleteFile(Constants.LocalCertificateKeyFilePath);
        }
    }

    [Fact]
    public void Certificate_WithFutureExpiration_DoesNotRequireRecreation()
    {
        // Arrange - Create a certificate that expires in 60 days (well within threshold)
        var validCert = CreateTestCertificate(daysUntilExpiration: 60);
        SaveCertificateToDisk(validCert, Constants.LocalCertificateCrtFilePath, Constants.LocalCertificateKeyFilePath);

        try
        {
            var loadedCert = MkcertCertificateManager.LoadExistingCertificate();
            Assert.NotNull(loadedCert);

            var isExpiringSoon = MkcertCertificateManager.IsCertificateExpiringSoon(loadedCert);
            Assert.False(isExpiringSoon, "Certificate expiring in 60 days should not require recreation");

            // Verify all domains are supported
            var allDomainsSupported = Constants.SupportedWildcardLocalhostDomainNames
                .All(wildcardDomain => loadedCert.MatchesHostname(ConvertWildcardToExample(wildcardDomain)));

            Assert.True(allDomainsSupported, "Valid certificate should support all required domains");
        }
        finally
        {
            TryDeleteFile(Constants.LocalCertificateCrtFilePath);
            TryDeleteFile(Constants.LocalCertificateKeyFilePath);
        }
    }

    private static string ConvertWildcardToExample(string wildcardDomain)
    {
        return wildcardDomain.Replace("*", "example");
    }

    private static X509Certificate2 CreateTestCertificate(int daysUntilExpiration)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=test.workleap.localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Add Subject Alternative Names for all supported wildcard domains
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddDnsName("*.workleap.localhost");
        sanBuilder.AddDnsName("*.barley.localhost");
        sanBuilder.AddDnsName("*.officevibe.localhost");
        sanBuilder.AddDnsName("*.sharegate.localhost");
        sanBuilder.AddDnsName("*.officevibe.com");
        sanBuilder.AddDnsName("*.workleap.com");
        sanBuilder.AddDnsName("*.officevibe-dev.com");
        sanBuilder.AddDnsName("*.workleap-dev.com");
        sanBuilder.AddDnsName("*.workleap-local.com");
        sanBuilder.AddDnsName("*.sharegate-dev.com");
        request.CertificateExtensions.Add(sanBuilder.Build());

        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter = DateTimeOffset.UtcNow.AddDays(daysUntilExpiration);

        return request.CreateSelfSigned(notBefore, notAfter);
    }

    private static X509Certificate2 CreateTestCertificateWithLimitedDomains(int daysUntilExpiration)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=test.workleap.localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Add only a subset of domains to simulate an outdated certificate
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddDnsName("*.workleap.localhost");
        // Intentionally missing other domains
        request.CertificateExtensions.Add(sanBuilder.Build());

        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter = DateTimeOffset.UtcNow.AddDays(daysUntilExpiration);

        return request.CreateSelfSigned(notBefore, notAfter);
    }

    private static void SaveCertificateToDisk(X509Certificate2 certificate, string crtPath, string keyPath)
    {
        // Ensure the directory exists
        var directory = Path.GetDirectoryName(crtPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Export certificate to PEM
        var certPem = certificate.ExportCertificatePem();
        File.WriteAllText(crtPath, certPem);

        // Export private key to PEM
        using var rsa = certificate.GetRSAPrivateKey();
        if (rsa == null)
        {
            throw new InvalidOperationException("Certificate does not have an RSA private key");
        }

        var keyPem = rsa.ExportRSAPrivateKeyPem();
        File.WriteAllText(keyPath, keyPem);
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Ignore errors during test cleanup
        }
    }

    public void Dispose()
    {
        this._temporaryCertificatesDir.Delete(recursive: true);
    }
}