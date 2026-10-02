using System.Xml.Linq;
using Dewiride.Erp.BuildingBlocks.Authentication.DataProtection;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.DataProtection;

public sealed class KeyRingStartupCheckTests
{
    private const string DataProtectionNamespace = "http://schemas.asp.net/2015/03/dataProtection";

    private const string KeyVaultDecryptor =
        "Azure.Extensions.AspNetCore.DataProtection.Keys.AzureKeyVaultXmlDecryptor, Azure.Extensions.AspNetCore.DataProtection.Keys, Version=1.6.4.0, Culture=neutral, PublicKeyToken=92742159e12e44c8";

    [Fact]
    public void IsWrappedByKeyVault_KeyWhoseSecretIsAKeyVaultEnvelope_IsTrue()
    {
        Assert.True(KeyRingStartupCheck.IsWrappedByKeyVault(Key(Secret(KeyVaultDecryptor, Envelope()))));
    }

    [Fact]
    public void IsWrappedByKeyVault_KeyVaultDecryptorNamedWithoutItsAssembly_IsTrue()
    {
        Assert.True(KeyRingStartupCheck.IsWrappedByKeyVault(Key(Secret("Azure.Extensions.AspNetCore.DataProtection.Keys.AzureKeyVaultXmlDecryptor", Envelope()))));
    }

    [Fact]
    public void IsWrappedByKeyVault_MasterKeyInTheClear_IsFalse()
    {
        Assert.False(KeyRingStartupCheck.IsWrappedByKeyVault(Key(MasterKey())));
    }

    [Fact]
    public void IsWrappedByKeyVault_KeyVaultEnvelopeBesideAMasterKeyInTheClear_IsFalse()
    {
        Assert.False(KeyRingStartupCheck.IsWrappedByKeyVault(Key(MasterKey(), Secret(KeyVaultDecryptor, Envelope()))));
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore.DataProtection.XmlEncryption.NullXmlDecryptor, Microsoft.AspNetCore.DataProtection, Version=10.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60")]
    [InlineData("Microsoft.AspNetCore.DataProtection.XmlEncryption.DpapiXmlDecryptor, Microsoft.AspNetCore.DataProtection, Version=10.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60")]
    [InlineData("Azure.Extensions.AspNetCore.DataProtection.Keys.AzureKeyVaultXmlDecryptorOfAnotherKind, Azure.Extensions.AspNetCore.DataProtection.Keys")]
    [InlineData("")]
    public void IsWrappedByKeyVault_SecretOfAnotherDecryptor_IsFalse(string decryptorType)
    {
        Assert.False(KeyRingStartupCheck.IsWrappedByKeyVault(Key(Secret(decryptorType, Envelope()))));
    }

    [Fact]
    public void IsWrappedByKeyVault_SecretWithoutADecryptorType_IsFalse()
    {
        Assert.False(KeyRingStartupCheck.IsWrappedByKeyVault(Key(new XElement(XName.Get("encryptedSecret", DataProtectionNamespace), Envelope()))));
    }

    [Fact]
    public void IsWrappedByKeyVault_KeyVaultEnvelopeBesideASecretOfAnotherDecryptor_IsFalse()
    {
        var other = Secret("Microsoft.AspNetCore.DataProtection.XmlEncryption.NullXmlDecryptor, Microsoft.AspNetCore.DataProtection", new XElement("unencryptedKey"));

        Assert.False(KeyRingStartupCheck.IsWrappedByKeyVault(Key(Secret(KeyVaultDecryptor, Envelope()), other)));
    }

    [Fact]
    public void IsWrappedByKeyVault_SecretOutsideTheDataProtectionNamespace_IsFalse()
    {
        Assert.False(KeyRingStartupCheck.IsWrappedByKeyVault(Key(new XElement("encryptedSecret", new XAttribute("decryptorType", KeyVaultDecryptor), Envelope()))));
    }

    [Fact]
    public void IsWrappedByKeyVault_KeyWithoutASecret_IsFalse()
    {
        Assert.False(KeyRingStartupCheck.IsWrappedByKeyVault(Key()));
    }

    [Fact]
    public void UnwrappedKeyMessage_File_NamesTheFileAndTheSetting()
    {
        var message = KeyRingStartupCheck.UnwrappedKeyMessage("key-0199a1b2-0000-7000-8000-000000000000.xml");

        Assert.Contains("key-0199a1b2-0000-7000-8000-000000000000.xml", message, StringComparison.Ordinal);
        Assert.Contains(KeyRingOptions.KeyIdentifierKey, message, StringComparison.Ordinal);
    }

    private static XElement Key(params object[] secrets) =>
        new("key",
            new XAttribute("id", Guid.CreateVersion7()),
            new XAttribute("version", 1),
            new XElement("creationDate", "2026-10-02T06:00:00Z"),
            new XElement("activationDate", "2026-10-02T06:00:00Z"),
            new XElement("expirationDate", "2026-12-31T06:00:00Z"),
            new XElement("descriptor",
                new XAttribute("deserializerType", "Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel.AuthenticatedEncryptorDescriptorDeserializer, Microsoft.AspNetCore.DataProtection"),
                new XElement("descriptor",
                    new XElement("encryption", new XAttribute("algorithm", "AES_256_CBC")),
                    new XElement("validation", new XAttribute("algorithm", "HMACSHA256")),
                    secrets)));

    private static XElement Secret(string decryptorType, XElement content) =>
        new(XName.Get("encryptedSecret", DataProtectionNamespace), new XAttribute("decryptorType", decryptorType), content);

    private static XElement Envelope() =>
        new("encryptedKey",
            new XElement("kid", "https://erp-test.vault.azure.net/keys/data-protection/0123456789abcdef0123456789abcdef"),
            new XElement("key", "d3JhcHBlZA=="),
            new XElement("iv", "aXY="),
            new XElement("value", "dmFsdWU="));

    private static XElement MasterKey() =>
        new("masterKey", new XAttribute(XName.Get("requiresEncryption", DataProtectionNamespace), "true"), new XElement("value", "c2VjcmV0IGluIHRoZSBjbGVhcg=="));
}
