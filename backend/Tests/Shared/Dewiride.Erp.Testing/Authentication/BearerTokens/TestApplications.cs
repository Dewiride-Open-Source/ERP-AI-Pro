namespace Dewiride.Erp.Testing.Authentication.BearerTokens;

public static class TestApplications
{
    public static TestApplication Integration { get; } = new(Guid.Parse("6c1f3e8a-9b2d-4f7e-a5c3-1d0e9f8b7a62"), Guid.Parse("b47e2c91-5d3a-4e8f-9c6b-2a1f0e9d8c73"), "ERP-AI-Pro test integration");

    public static TestApplication NativeClient { get; } = new(Guid.Parse("e2a9c4f7-3b1d-4c6e-8f5a-7d2b1c0e9f84"), Guid.Parse("4d8b6f2a-1c9e-4a3d-b7f5-6e0c2d1a9b95"), "ERP-AI-Pro test native client");
}
