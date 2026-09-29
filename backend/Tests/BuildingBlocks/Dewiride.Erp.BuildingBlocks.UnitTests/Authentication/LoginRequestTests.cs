using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.BuildingBlocks.Authentication.Endpoints.Requests;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication;

public sealed class LoginRequestTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("/platform/attachments")]
    [InlineData("/invoices?status=draft&page=2")]
    [InlineData("/caf%C3%A9#totals")]
    public void IsLocalPath_PathOnThisSite_IsAccepted(string? returnUrl)
    {
        Assert.True(LoginRequest.IsLocalPath(returnUrl));
    }

    [Theory]
    [InlineData("https://evil.example.com/")]
    [InlineData("http://evil.example.com")]
    [InlineData("//evil.example.com")]
    [InlineData("/\\evil.example.com")]
    [InlineData("\\\\evil.example.com")]
    [InlineData("/\t/evil.example.com")]
    [InlineData("/\r\nLocation: https://evil.example.com")]
    [InlineData("~/platform")]
    [InlineData("~/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("platform/attachments")]
    [InlineData("/café")]
    [InlineData("/two words")]
    public void IsLocalPath_AnythingButAPathOnThisSite_IsRefused(string returnUrl)
    {
        Assert.False(LoginRequest.IsLocalPath(returnUrl));
    }

    [Fact]
    public void Validate_NonLocalReturnUrl_NamesTheReturnUrlMember()
    {
        var results = Validate(new LoginRequest("//evil.example.com"));

        var result = Assert.Single(results);
        Assert.Equal(LoginRequest.NotLocalMessage, result.ErrorMessage);
        Assert.Equal([nameof(LoginRequest.ReturnUrl)], result.MemberNames);
    }

    [Fact]
    public void Validate_ReturnUrlOverTheMaximumLength_FailsTheLengthRule()
    {
        var results = Validate(new LoginRequest("/" + new string('a', LoginRequest.MaxReturnUrlLength)));

        var result = Assert.Single(results);
        Assert.Equal([nameof(LoginRequest.ReturnUrl)], result.MemberNames);
        Assert.Contains(LoginRequest.MaxReturnUrlLength.ToString(System.Globalization.CultureInfo.InvariantCulture), result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ReturnUrlOfTheMaximumLength_Succeeds()
    {
        Assert.Empty(Validate(new LoginRequest("/" + new string('a', LoginRequest.MaxReturnUrlLength - 1))));
    }

    [Theory]
    [InlineData(null, LoginRequest.DefaultReturnUrl)]
    [InlineData("", LoginRequest.DefaultReturnUrl)]
    [InlineData("/platform/attachments", "/platform/attachments")]
    public void LocalReturnUrl_Always_IsTheSuppliedPathOrTheHomePage(string? returnUrl, string expected)
    {
        Assert.Equal(expected, new LoginRequest(returnUrl).LocalReturnUrl);
    }

    private static List<ValidationResult> Validate(LoginRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        return results;
    }
}
