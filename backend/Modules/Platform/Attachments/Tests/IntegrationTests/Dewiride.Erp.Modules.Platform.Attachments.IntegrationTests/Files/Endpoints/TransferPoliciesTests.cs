using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints;
using Dewiride.Erp.Testing;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.Modules.Platform.Attachments.IntegrationTests.Files.Endpoints;

public sealed class TransferPoliciesTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Theory]
    [InlineData("Platform.Attachments.Upload")]
    [InlineData(TransferEndpoints.DownloadRouteName)]
    public void TransferEndpoint_Metadata_UsesTheTransferTimeoutPolicy(string routeName)
    {
        var timeout = EndpointNamed(routeName).Metadata.GetMetadata<RequestTimeoutAttribute>();

        Assert.Equal(TransferEndpoints.TimeoutPolicy, timeout?.PolicyName);
    }

    [Fact]
    public void TransferTimeoutPolicy_Registered_LastsTheConfiguredTransferTimeout()
    {
        var transferTimeout = factory.Services.GetRequiredService<IOptions<AttachmentsOptions>>().Value.TransferTimeout;

        var policy = factory.Services.GetRequiredService<IOptions<RequestTimeoutOptions>>().Value.Policies[TransferEndpoints.TimeoutPolicy];

        Assert.Equal(transferTimeout, policy.Timeout);
    }

    [Fact]
    public void UploadEndpoint_Metadata_RequiresTheUploadConcurrencyPolicy()
    {
        var limiting = EndpointNamed("Platform.Attachments.Upload").Metadata.GetMetadata<EnableRateLimitingAttribute>();

        Assert.Equal(TransferEndpoints.UploadConcurrencyPolicy, limiting?.PolicyName);
    }

    [Theory]
    [InlineData("Platform.Attachments.List")]
    [InlineData("Platform.Attachments.CreateDownloadLink")]
    public void OtherEndpoint_Metadata_KeepsTheDefaultTimeoutAndNoUploadCap(string routeName)
    {
        var endpoint = EndpointNamed(routeName);

        Assert.Null(endpoint.Metadata.GetMetadata<RequestTimeoutAttribute>());
        Assert.Null(endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>());
    }

    private RouteEndpoint EndpointNamed(string routeName) =>
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == routeName);
}
