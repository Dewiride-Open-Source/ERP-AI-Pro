using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Dewiride.Erp.BuildingBlocks.Application.Actors.PeopleDirectory;
using Dewiride.Erp.BuildingBlocks.Authentication.Graph;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.Graph;

public sealed class GraphPeopleDirectoryPhotoTests : IDisposable
{
    private const string SizedPhotoPath = "/v1.0/me/photos/240x240/$value";

    private const string LargestPhotoPath = "/v1.0/me/photo/$value";

    private static readonly byte[] Png = Convert.FromHexString("89504E470D0A1A0A0000000D49484452");

    private static readonly byte[] Jpeg = Convert.FromHexString("FFD8FFE000104A464946");

    private readonly FakeLogger<GraphPeopleDirectory> _logger = new();

    private readonly InProcessCache _caches = new();

    private readonly ClaimsPrincipal _person = Person();

    [Fact]
    public async Task OwnPhotoAsync_PhotoOfTheSizeThePagesShow_ReadsItOnBehalfOfThePersonWithTheirOwnProfilePermission()
    {
        var graph = new StubGraph(_ => Image(Png, "image/png"));
        var headers = new StubAuthorizationHeaders();

        var photo = await Directory(graph, headers).OwnPhotoAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Png, photo.Value.Content);
        Assert.Equal("image/png", photo.Value.ContentType);
        var request = Assert.Single(graph.Requests);
        Assert.Equal(SizedPhotoPath, request.Address.AbsolutePath);
        Assert.Equal(StubAuthorizationHeaders.AuthorizationHeader, request.Authorization);
        Assert.Equal([MicrosoftGraph.ReadOwnProfileScope], headers.Scopes);
        Assert.Equal(OpenIdConnectDefaults.AuthenticationScheme, headers.Options?.AcquireTokenOptions.AuthenticationOptionsName);
        Assert.Same(_person, headers.Principal);
    }

    [Fact]
    public async Task OwnPhotoAsync_NoPhotoOfThatSize_ReadsTheLargestPhoto()
    {
        var graph = new StubGraph(request => request.RequestUri!.AbsolutePath == SizedPhotoPath ? ImageNotFound() : Image(Jpeg, "image/jpeg"));

        var photo = await Directory(graph).OwnPhotoAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Jpeg, photo.Value.Content);
        Assert.Equal("image/jpeg", photo.Value.ContentType);
        Assert.Equal([SizedPhotoPath, LargestPhotoPath], graph.Requests.Select(request => request.Address.AbsolutePath));
    }

    [Fact]
    public async Task OwnPhotoAsync_NoPhotoAtAll_AnswersPhotoNotFound()
    {
        var graph = new StubGraph(_ => ImageNotFound());

        var photo = await Directory(graph).OwnPhotoAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.PhotoNotFound, photo.Error);
        Assert.Equal([SizedPhotoPath, LargestPhotoPath], graph.Requests.Select(request => request.Address.AbsolutePath));
    }

    [Fact]
    public async Task OwnPhotoAsync_AskedAgainWhileTheAnswerIsKept_AnswersThePhotoWithoutAskingGraph()
    {
        var graph = new StubGraph(_ => Image(Png, "image/png"));
        await Directory(graph).OwnPhotoAsync(TestContext.Current.CancellationToken);

        var again = await Directory(graph).OwnPhotoAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Png, again.Value.Content);
        Assert.Equal("image/png", again.Value.ContentType);
        Assert.Single(graph.Requests);
    }

    [Fact]
    public async Task OwnPhotoAsync_PersonWithoutAPhotoAskedAgain_AnswersPhotoNotFoundWithoutAskingGraph()
    {
        var graph = new StubGraph(_ => ImageNotFound());
        await Directory(graph).OwnPhotoAsync(TestContext.Current.CancellationToken);

        var again = await Directory(graph).OwnPhotoAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.PhotoNotFound, again.Error);
        Assert.Equal(2, graph.Requests.Count);
    }

    [Fact]
    public async Task OwnPhotoAsync_AnotherPerson_AsksGraphForTheirOwnPhoto()
    {
        var graph = new StubGraph(_ => Image(Png, "image/png"));
        await Directory(graph).OwnPhotoAsync(TestContext.Current.CancellationToken);

        await Directory(graph, person: Person()).OwnPhotoAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, graph.Requests.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "directory.access-denied")]
    [InlineData(HttpStatusCode.Forbidden, "directory.access-denied")]
    [InlineData(HttpStatusCode.TooManyRequests, "directory.unavailable")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "directory.unavailable")]
    public async Task OwnPhotoAsync_GraphRefusesOrFails_AnswersTheErrorAndAsksGraphAgainNextTime(HttpStatusCode status, string code)
    {
        var graph = new StubGraph(_ => Json(status, """{"error":{"code":"Sample_Code","message":"The photo of Meera Nair cannot be read."}}"""));

        var first = await Directory(graph).OwnPhotoAsync(TestContext.Current.CancellationToken);
        var second = await Directory(graph).OwnPhotoAsync(TestContext.Current.CancellationToken);

        Assert.Equal(code, first.Error?.Code);
        Assert.Equal(code, second.Error?.Code);
        Assert.Equal(2, graph.Requests.Count);
        var record = _logger.Collector.GetSnapshot()[0];
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Contains("photo", record.Message, StringComparison.Ordinal);
        Assert.Contains("Sample_Code", record.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Meera", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OwnPhotoAsync_EntraAsksThePersonToSignInAgain_AnswersAccessDeniedWithoutAskingGraph()
    {
        var graph = new StubGraph(_ => Image(Png, "image/png"));
        var refusal = new MsalUiRequiredException("invalid_grant", "AADSTS65001: consent required.", null, UiRequiredExceptionClassification.ConsentRequired);
        var headers = new StubAuthorizationHeaders(new MicrosoftIdentityWebChallengeUserException(refusal, [MicrosoftGraph.ReadOwnProfileScope], null));

        var photo = await Directory(graph, headers).OwnPhotoAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.AccessDenied, photo.Error);
        Assert.Empty(graph.Requests);
    }

    [Fact]
    public async Task OwnPhotoAsync_ImageOfAnotherTypeThanGraphNames_AnswersTheTypeItsBytesShow()
    {
        var graph = new StubGraph(_ => Image(Jpeg, "image/png"));

        var photo = await Directory(graph).OwnPhotoAsync(TestContext.Current.CancellationToken);

        Assert.Equal("image/jpeg", photo.Value.ContentType);
    }

    [Fact]
    public async Task OwnPhotoAsync_AnswerThatIsNoImageTheErpShows_Throws()
    {
        var graph = new StubGraph(_ => Image(Encoding.UTF8.GetBytes("<html>Sign in</html>"), "image/png"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Directory(graph).OwnPhotoAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OwnPhotoAsync_PhotoLargerThanTheErpKeeps_Throws()
    {
        var large = new byte[ProfilePhotos.MaxBytes + 1];
        Png.CopyTo(large, 0);
        var graph = new StubGraph(_ => Image(large, "image/png"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Directory(graph).OwnPhotoAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OwnPhotoAsync_AnswerThatBreaksOffWhileItIsRead_AnswersUnavailableAndAsksGraphAgainNextTime()
    {
        var graph = new StubGraph(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new BreakingStream()) });

        var first = await Directory(graph).OwnPhotoAsync(TestContext.Current.CancellationToken);
        await Directory(graph).OwnPhotoAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.Unavailable, first.Error);
        Assert.Equal(2, graph.Requests.Count);
        Assert.Equal(LogLevel.Warning, _logger.Collector.GetSnapshot()[0].Level);
    }

    [Fact]
    public async Task OwnPhotoAsync_NobodySignedIn_Throws()
    {
        var graph = new StubGraph(_ => throw new InvalidOperationException("Graph is never asked."));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Directory(graph, person: new ClaimsPrincipal(new ClaimsIdentity())).OwnPhotoAsync(TestContext.Current.CancellationToken));
    }

    public void Dispose() => _caches.Dispose();

    private static ClaimsPrincipal Person() => new(new ClaimsIdentity([new Claim("oid", Guid.CreateVersion7().ToString("D"))], "Cookies"));

    private static HttpResponseMessage Image(byte[] content, string contentType)
    {
        var body = new ByteArrayContent(content);
        body.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = body };
    }

    private static HttpResponseMessage ImageNotFound() =>
        Json(HttpStatusCode.NotFound, """{"error":{"code":"ImageNotFound","message":"Exception of type 'Microsoft.Fast.Profile.Core.Exception.ImageNotFoundException' was thrown."}}""");

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private GraphPeopleDirectory Directory(StubGraph graph, StubAuthorizationHeaders? headers = null, ClaimsPrincipal? person = null)
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = person ?? _person } };
#pragma warning disable CA2000 // The directory owns the client for the test, as the HTTP client factory's typed client does.
        var client = new HttpClient(graph) { BaseAddress = MicrosoftGraph.BaseAddress };
#pragma warning restore CA2000

        return new GraphPeopleDirectory(client, headers ?? new StubAuthorizationHeaders(), accessor, _caches.Cache, _logger);
    }

    private sealed class BreakingStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("The connection was reset.");

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("The connection was reset."));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
