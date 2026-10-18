using System.Net;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.EndToEnd;

namespace Dewiride.Erp.Host.Api.IntegrationTests.EndToEnd;

public sealed class DirectoryPeopleTests(DirectoryPeopleTests.Fixture fixture) : IClassFixture<DirectoryPeopleTests.Fixture>
{
    private const string PhotoRoute = "/api/identity/users/me/photo";

    private static readonly byte[] Png = Convert.FromHexString("89504E470D0A1A0A0000000D49484452");

    [Fact]
    public async Task Post_PersonWithAPhotoSentWithTheirSessionAndNoRequestToken_AddsThemSoTheirSignInShowsThePhoto()
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        var person = await SignInAsync(client);

        using var added = await client.PostAsJsonAsync(new Uri(DirectoryPeople.Path, UriKind.Relative), Seed(person.Id, Photo("image/png", Convert.ToBase64String(Png))), TestContext.Current.CancellationToken);
        using var photo = await client.GetAsync(new Uri(PhotoRoute, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, added.StatusCode);
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/png", photo.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await photo.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Post_WithoutASession_AddsThePerson()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        var person = await SignInAsync(client);
        using var anonymous = fixture.Factory.CreateClient();

        using var added = await anonymous.PostAsJsonAsync(new Uri(DirectoryPeople.Path, UriKind.Relative), Seed(person.Id, Photo("image/png", Convert.ToBase64String(Png))), TestContext.Current.CancellationToken);
        using var photo = await client.GetAsync(new Uri(PhotoRoute, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, added.StatusCode);
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
    }

    [Fact]
    public async Task Post_PersonWithoutAPhoto_AddsThemSoTheirSignInShowsNone()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        var person = await SignInAsync(client);

        using var added = await client.PostAsJsonAsync(new Uri(DirectoryPeople.Path, UriKind.Relative), Seed(person.Id, photo: null), TestContext.Current.CancellationToken);
        using var photo = await client.GetAsync(new Uri(PhotoRoute, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, added.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, photo.StatusCode);
    }

    [Fact]
    public async Task Post_PersonWithoutAnObjectIdANameOrASignInName_Answers400ForEach()
    {
        using var anonymous = fixture.Factory.CreateClient();

        using var response = await anonymous.PostAsJsonAsync(new Uri(DirectoryPeople.Path, UriKind.Relative), new { objectId = Guid.Empty, displayName = " " }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal(["displayName", "objectId", "userPrincipalName"], problem!.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("image/svg+xml", "PHN2Zz48L3N2Zz4=")]
    [InlineData("image/png", "not base64")]
    [InlineData("image/png", "")]
    public async Task Post_PhotoThatIsNoImageOfTheDirectoryInBase64_Answers400KeyedByThePhoto(string contentType, string base64)
    {
        using var anonymous = fixture.Factory.CreateClient();

        using var response = await anonymous.PostAsJsonAsync(new Uri(DirectoryPeople.Path, UriKind.Relative), Seed(Guid.CreateVersion7(), Photo(contentType, base64)), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("photo", Assert.Single(problem!.Errors.Keys));
    }

    [Fact]
    public async Task Map_InTheDevelopmentEnvironment_StopsTheHostFromStarting()
    {
        await using var factory = new ErpApiFactory().WithTestEndpoints(DirectoryPeople.Map);

        var exception = Assert.IsType<InvalidOperationException>(factory.StartFailure());

        Assert.Contains(EndToEndHost.EnvironmentName, exception.Message, StringComparison.Ordinal);
    }

    private static object Seed(Guid objectId, object? photo) =>
        new { objectId, displayName = "Meera Nair", userPrincipalName = $"meera.nair.{objectId:N}@dewiride.test", photo };

    private static object Photo(string contentType, string base64) => new { contentType, base64 };

    private static async Task<PersonaSignInResponse> SignInAsync(HttpClient client)
    {
        using var response = await client.PostAsync(new Uri($"{PersonaSignIn.PathPrefix}/accountant", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var person = await response.Content.ReadFromJsonAsync<PersonaSignInResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(person);

        return person;
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = ErpApiFactory.ForEnvironment(EndToEndHost.EnvironmentName)
            .WithTestEndpoints(PersonaSignIn.Map)
            .WithTestEndpoints(DirectoryPeople.Map);

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
