using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.BuildingBlocks.Attachments.Options;
using Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Encryption;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Options;

public sealed class AttachmentsOptionsValidatorTests
{
    private const string Section = AttachmentsOptions.SectionName;

    private readonly AttachmentsOptionsValidator _validator = new();

    [Fact]
    public void Validate_DefaultsWithAnEmulatorAndAKey_Succeeds()
    {
        var result = _validator.Validate(null, ValidOptions());

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Theory]
    [InlineData("https://erpaipro.blob.core.windows.net/")]
    [InlineData("https://erpaipro.blob.core.windows.net")]
    [InlineData("HTTPS://ERPAIPRO.BLOB.CORE.WINDOWS.NET/")]
    [InlineData("https://erpaipro.blob.core.windows.net:443/")]
    public void Validate_HttpsBlobEndpointWithoutAnEmulator_Succeeds(string serviceUri)
    {
        var result = _validator.Validate(null, ValidOptions(serviceUri: serviceUri, emulatorHost: null));

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", " ")]
    [InlineData("https://erpaipro.blob.core.windows.net/", "127.0.0.1")]
    public void Validate_NotExactlyOneStorageTarget_FailsNamingBoth(string? serviceUri, string? emulatorHost)
    {
        var result = _validator.Validate(null, ValidOptions(serviceUri: serviceUri, emulatorHost: emulatorHost));

        Assert.True(result.Failed);
        Assert.Contains($"Set exactly one of {Section}:BlobServiceUri", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains($"{Section}:EmulatorHost", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://erpaipro.blob.core.windows.net/")]
    [InlineData("https://erpaipro.blob.core.windows.net/attachments")]
    [InlineData("https://erpaipro.blob.core.windows.net/?sv=2026-06-06")]
    [InlineData("https://reader@erpaipro.blob.core.windows.net/")]
    [InlineData("ftp://erpaipro.blob.core.windows.net/")]
    [InlineData("erpaipro.blob.core.windows.net")]
    [InlineData("/attachments")]
    [InlineData("not a uri")]
    public void Validate_BlobServiceUriNotABareHttpsEndpoint_Fails(string serviceUri)
    {
        var result = _validator.Validate(null, ValidOptions(serviceUri: serviceUri, emulatorHost: null));

        Assert.True(result.Failed);
        Assert.Contains($"{Section}:BlobServiceUri must be the https blob endpoint of the storage account", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("localhost")]
    [InlineData("LOCALHOST")]
    [InlineData("host.docker.internal")]
    [InlineData("azurite")]
    [InlineData(" azurite ")]
    [InlineData("blob-emulator-2")]
    public void Validate_LocalOrComposeServiceEmulatorHost_Succeeds(string emulatorHost)
    {
        var result = _validator.Validate(null, ValidOptions(emulatorHost: emulatorHost));

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Theory]
    [InlineData("10.0.0.5")]
    [InlineData("::1")]
    [InlineData("storage.example.com")]
    [InlineData("azurite:10000")]
    [InlineData("Azurite")]
    [InlineData("2azurite")]
    [InlineData("-azurite")]
    [InlineData("http://127.0.0.1")]
    [InlineData("azurite/devstoreaccount1")]
    public void Validate_OtherEmulatorHost_FailsQuotingIt(string emulatorHost)
    {
        var result = _validator.Validate(null, ValidOptions(emulatorHost: emulatorHost));

        Assert.True(result.Failed);
        Assert.Contains($"{Section}:EmulatorHost must be 127.0.0.1, localhost, host.docker.internal", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains($"'{emulatorHost}' was rejected", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("attachments")]
    [InlineData("erp-attachments-2026")]
    [InlineData("0a9")]
    public void Validate_ValidContainerName_Succeeds(string containerName)
    {
        var options = ValidOptions();
        options.ContainerName = containerName;

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("Attachments")]
    [InlineData("-attachments")]
    [InlineData("attachments-")]
    [InlineData("erp--attachments")]
    [InlineData("erp_attachments")]
    [InlineData("erp.attachments")]
    [InlineData(" attachments")]
    public void Validate_InvalidContainerName_FailsStatingTheRule(string? containerName)
    {
        var options = ValidOptions();
        options.ContainerName = containerName!;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            $"{Section}:ContainerName must be 3 to 63 lowercase letters, digits and single hyphens, starting and ending with a letter or digit.",
            result.FailureMessage,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(63, true)]
    [InlineData(64, false)]
    public void Validate_ContainerNameLength_AllowsThreeToSixtyThreeCharacters(int length, bool valid)
    {
        var options = ValidOptions();
        options.ContainerName = new string('a', length);

        var result = _validator.Validate(null, options);

        Assert.Equal(valid, result.Succeeded);
    }

    [Theory]
    [InlineData("00:10:00", "00:20:00", true)]
    [InlineData("00:10:00", "01:00:00", true)]
    [InlineData("00:00:30", "00:01:00", true)]
    [InlineData("00:10:00", "00:19:59", false)]
    [InlineData("00:05:00", "00:09:00", false)]
    public void Validate_ReservationLifetimeAgainstTheTransferTimeout_RequiresTwice(string transferTimeout, string reservationLifetime, bool valid)
    {
        var options = ValidOptions();
        options.TransferTimeout = TimeSpan.Parse(transferTimeout, CultureInfo.InvariantCulture);
        options.UploadReservationLifetime = TimeSpan.Parse(reservationLifetime, CultureInfo.InvariantCulture);

        var result = _validator.Validate(null, options);

        Assert.Equal(valid, result.Succeeded);
        Assert.Equal(
            !valid,
            result.FailureMessage?.Contains($"{Section}:UploadReservationLifetime must be at least twice {Section}:TransferTimeout", StringComparison.Ordinal) ?? false);
    }

    [Fact]
    public void Validate_AllowedContentTypeWithoutACheck_FailsNamingIt()
    {
        var options = ValidOptions();
        options.AllowedContentTypes = "application/pdf;application/x-msdownload";

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("'application/x-msdownload'", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_EncryptionKeyMissing_FailsWithTheKeyProblem()
    {
        var options = ValidOptions();
        options.EncryptionKey = null;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains($"{Section}:EncryptionKey is required", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_MalformedRetiredKey_FailsNamingTheEntry()
    {
        var options = ValidOptions();
        options.RetiredEncryptionKeys = $"{TestKeys.Setting(TestKeys.Create())};{TestKeys.NewId()}:tooshort";

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains($"{Section}:RetiredEncryptionKeys entry 2 is not", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_SeveralProblems_ReportsEachOfThem()
    {
        var options = ValidOptions(emulatorHost: null);
        options.ContainerName = "Bad_Name";
        options.AllowedContentTypes = string.Empty;
        options.UploadReservationLifetime = options.TransferTimeout;
        options.EncryptionKey = null;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Equal(5, result.Failures.Count());
    }

    [Fact]
    public void Validate_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _validator.Validate(null, null!));
    }

    private static AttachmentsOptions ValidOptions(string? serviceUri = null, string? emulatorHost = "127.0.0.1") =>
        new() { BlobServiceUri = serviceUri, EmulatorHost = emulatorHost, EncryptionKey = TestKeys.Setting(TestKeys.Create()) };
}
