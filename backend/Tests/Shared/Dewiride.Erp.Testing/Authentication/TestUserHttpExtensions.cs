namespace Dewiride.Erp.Testing.Authentication;

public static class TestUserHttpExtensions
{
    public static HttpClient AsUser(this HttpClient client, TestUser user)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(user);

        client.DefaultRequestHeaders.Remove(TestAuthHandler.UserHeader);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, user.ObjectId.ToString("D"));

        return client;
    }

    public static HttpRequestMessage AsUser(this HttpRequestMessage request, TestUser user)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(user);

        request.Headers.Remove(TestAuthHandler.UserHeader);
        request.Headers.Add(TestAuthHandler.UserHeader, user.ObjectId.ToString("D"));

        return request;
    }
}
