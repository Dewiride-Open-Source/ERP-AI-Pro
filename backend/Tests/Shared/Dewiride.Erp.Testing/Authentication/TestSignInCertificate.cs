using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Dewiride.Erp.Testing.Authentication;

// One throwaway self-signed certificate per test process; MSAL signs client assertions with it only for the test token
// endpoint, so Entra never sees it.
public static class TestSignInCertificate
{
    private static readonly Lazy<string> Current = new(() =>
        Create(TimeProvider.System.GetUtcNow().AddDays(-1), TimeProvider.System.GetUtcNow().AddYears(1)));

    public static string Base64 => Current.Value;

    public static string Create(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=ERP-AI-Pro test sign-in", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(notBefore, notAfter);

        return Convert.ToBase64String(certificate.Export(X509ContentType.Pkcs12));
    }
}
