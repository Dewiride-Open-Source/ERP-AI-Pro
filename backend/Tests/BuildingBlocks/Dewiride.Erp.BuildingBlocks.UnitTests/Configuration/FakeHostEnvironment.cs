using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Configuration;

internal sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;

    public string ApplicationName { get; set; } = "Dewiride.Erp.BuildingBlocks.UnitTests";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
