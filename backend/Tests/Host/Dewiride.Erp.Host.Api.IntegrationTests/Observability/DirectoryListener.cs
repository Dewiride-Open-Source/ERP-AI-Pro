using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Dewiride.Erp.BuildingBlocks.Authentication.Graph;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Graph;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Observability;

// The runtime records the client activity of an HTTP call, and redacts its url.full, only inside SocketsHttpHandler, so a
// directory answered in memory leaves no such span to inspect. This listener answers for a TestDirectory over TLS on a
// loopback port, and the handler it creates connects every request there while the request's address still names Graph.
internal sealed class DirectoryListener : IAsyncDisposable
{
    private readonly WebApplication _app;

    private readonly X509Certificate2 _certificate;

    private TestDirectory? _directory;

    private DirectoryListener(WebApplication app, X509Certificate2 certificate)
    {
        _app = app;
        _certificate = certificate;
    }

    public int Port { get; private set; }

    public static async Task<DirectoryListener> StartAsync()
    {
        var certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(TestSignInCertificate.Base64), password: null);
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
        var app = builder.Build();
        var listener = new DirectoryListener(app, certificate);
        app.Run(listener.AnswerAsync);
        await app.StartAsync(TestContext.Current.CancellationToken);
        listener.Port = new Uri(Assert.Single(app.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>().Addresses)).Port;

        return listener;
    }

    public void Serve(TestDirectory directory) => _directory = directory;

    public HttpMessageHandler CreateHandler() => new SocketsHttpHandler
    {
        ConnectCallback = async (_, cancellationToken) =>
        {
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, Port), cancellationToken);

                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
        SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, presented, _, _) => IsTheListenersOwn(presented) },
    };

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
        _certificate.Dispose();
    }

    private bool IsTheListenersOwn(X509Certificate? presented) =>
        presented is not null
        && string.Equals(presented.GetCertHashString(HashAlgorithmName.SHA256), _certificate.GetCertHashString(HashAlgorithmName.SHA256), StringComparison.Ordinal);

    private async Task AnswerAsync(HttpContext context)
    {
        var directory = _directory ?? throw new InvalidOperationException("The listener answers for no directory yet; call Serve first.");
        using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), new Uri($"{Uri.UriSchemeHttps}://{MicrosoftGraph.BaseAddress.Host}{context.Request.Path}{context.Request.QueryString}"));
        foreach (var header in context.Request.Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }

        using var invoker = new HttpMessageInvoker(directory.CreateHandler());
        using var response = await invoker.SendAsync(request, context.RequestAborted);
        context.Response.StatusCode = (int)response.StatusCode;
        context.Response.ContentType = response.Content.Headers.ContentType?.ToString();
        await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
    }
}
