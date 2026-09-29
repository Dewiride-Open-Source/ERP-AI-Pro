using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Certificates;

// Key Vault exports a certificate as a passwordless base64 PKCS#12; Microsoft.Identity.Web reads the same value when it
// first redeems a code, so a value that fails here would otherwise fail only once someone tries to sign in.
internal static class SignInCertificate
{
    public static string? FindProblem(string value, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(value);

        byte[] pkcs12;
        try
        {
            pkcs12 = Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            return "is not base64 text.";
        }

        X509Certificate2 certificate;
        try
        {
            certificate = X509CertificateLoader.LoadPkcs12(pkcs12, password: null, X509KeyStorageFlags.EphemeralKeySet);
        }
        catch (CryptographicException)
        {
            return "is not a PKCS#12 certificate that opens without a password.";
        }

        using (certificate)
        {
            using var key = certificate.GetRSAPrivateKey();
            if (key is null)
            {
                return "carries no RSA private key.";
            }

            var notBefore = new DateTimeOffset(certificate.NotBefore.ToUniversalTime(), TimeSpan.Zero);
            var notAfter = new DateTimeOffset(certificate.NotAfter.ToUniversalTime(), TimeSpan.Zero);
            if (now < notBefore || now >= notAfter)
            {
                return string.Create(CultureInfo.InvariantCulture, $"is not valid now ({now:O}): it is valid from {notBefore:O} to {notAfter:O}.");
            }
        }

        return null;
    }
}
