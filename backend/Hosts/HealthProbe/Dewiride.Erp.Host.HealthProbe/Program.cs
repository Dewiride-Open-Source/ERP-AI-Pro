using System.Net;

var port = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")?.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "8080";
var path = args.Length > 0 ? args[0] : "/healthz/live";

using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

try
{
    using var response = await client.GetAsync(new Uri($"http://127.0.0.1:{port}{path}"));
    return response.StatusCode == HttpStatusCode.OK ? 0 : 1;
}
catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
{
    return 1;
}
