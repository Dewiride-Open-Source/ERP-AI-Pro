using System.Reflection;
using Dewiride.Erp.BuildingBlocks.Persistence.Retention;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Retention;

// Every host registers the sweeps its building blocks declare, and only the migrator registers the runner, because the API's
// database user may not delete from the audit schema.
public sealed class RetentionRegistrationTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public void Services_Api_RegisterTheSweepsButNoRunner()
    {
        Assert.NotEmpty(factory.Services.GetServices<RetentionSweepRegistration>());
        Assert.False(factory.Services.GetRequiredService<IServiceProviderIsService>().IsService(typeof(RetentionRunner)));
        Assert.Null(factory.Services.GetService<RetentionRunner>());
    }

    [Fact]
    public void HostedServices_Api_NeitherAreNorTakeASweepOrTheRunner()
    {
        var hosted = factory.Services.GetServices<IHostedService>().Select(service => service.GetType()).ToList();

        Assert.True(TakesSweeps(typeof(RetentionRunner)));
        Assert.NotEmpty(hosted);
        Assert.Empty(hosted.Where(type => Mentions(type) || TakesSweeps(type)).Select(type => type.FullName));
    }

    private static bool TakesSweeps(Type type) =>
        type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => Mentions(parameter.ParameterType));

    private static bool Mentions(Type type) =>
        type == typeof(RetentionRunner)
        || type == typeof(RetentionSweepRegistration)
        || typeof(IRetentionSweep).IsAssignableFrom(type)
        || type.GetGenericArguments().Any(Mentions);
}
