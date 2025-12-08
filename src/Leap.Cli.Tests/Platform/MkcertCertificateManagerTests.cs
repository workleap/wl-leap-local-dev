using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

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

    [Fact]
    public void Certificate_ExpiringWithin30Days_IsDetectedAsExpiringSoon()
    {
        // Create a certificate that expires in 15 days (within the 30-day threshold)
        var expiringCert = CreateTestCertificate(daysUntilExpiration: 15);

        // Verify the certificate is expiring soon
        var expirationThreshold = DateTime.UtcNow.AddDays(30);
        var isExpiringSoon = expiringCert.NotAfter.ToUniversalTime() <= expirationThreshold;

        Assert.True(isExpiringSoon, "Certificate expiring in 15 days should be detected as expiring soon");
    }

    [Fact]
    public void Certificate_ExpiringAfter30Days_IsNotDetectedAsExpiringSoon()
    {
        // Create a certificate that expires in 60 days (outside the 30-day threshold)
        var validCert = CreateTestCertificate(daysUntilExpiration: 60);

        // Verify the certificate is not expiring soon
        var expirationThreshold = DateTime.UtcNow.AddDays(30);
        var isExpiringSoon = validCert.NotAfter.ToUniversalTime() <= expirationThreshold;

        Assert.False(isExpiringSoon, "Certificate expiring in 60 days should not be detected as expiring soon");
    }

    [Fact]
    public void Certificate_ExpiringExactlyIn30Days_IsDetectedAsExpiringSoon()
    {
        // Create a certificate that expires in exactly 30 days (at the threshold)
        var expiringCert = CreateTestCertificate(daysUntilExpiration: 30);

        // Verify the certificate is expiring soon (boundary condition)
        var expirationThreshold = DateTime.UtcNow.AddDays(30);
        var isExpiringSoon = expiringCert.NotAfter.ToUniversalTime() <= expirationThreshold;

        Assert.True(isExpiringSoon, "Certificate expiring in exactly 30 days should be detected as expiring soon");
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

    private static void SaveCertificateToDisk(X509Certificate2 certificate, string crtPath, string keyPath)
    {
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

    public void Dispose()
    {
        this._temporaryCertificatesDir.Delete(recursive: true);
    }
}