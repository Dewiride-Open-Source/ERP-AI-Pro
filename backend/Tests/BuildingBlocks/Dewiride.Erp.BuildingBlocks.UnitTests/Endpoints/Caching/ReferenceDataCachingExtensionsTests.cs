using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Caching;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.Caching;

public sealed class ReferenceDataCachingExtensionsTests
{
    [Fact]
    public void WithReferenceDataCaching_MaxAge_AddsTheMetadata()
    {
        var builder = new RecordingConventionBuilder();

        builder.WithReferenceDataCaching(TimeSpan.FromMinutes(10));

        Assert.Equal(new ReferenceDataCacheMetadata(TimeSpan.FromMinutes(10)), Assert.Single(builder.Build().Metadata.OfType<ReferenceDataCacheMetadata>()));
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("00:00:00.500")]
    [InlineData("1.00:00:01")]
    public void WithReferenceDataCaching_MaxAgeOutsideOneSecondToOneDay_Throws(string maxAge)
    {
        var builder = new RecordingConventionBuilder();

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithReferenceDataCaching(TimeSpan.Parse(maxAge, CultureInfo.InvariantCulture)));
    }

    private sealed class RecordingConventionBuilder : IEndpointConventionBuilder
    {
        private readonly List<Action<EndpointBuilder>> _conventions = [];

        public void Add(Action<EndpointBuilder> convention) => _conventions.Add(convention);

        public RouteEndpointBuilder Build()
        {
            var builder = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse("/"), 0);
            foreach (var convention in _conventions)
            {
                convention(builder);
            }

            return builder;
        }
    }
}
