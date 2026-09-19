using Dewiride.Erp.BuildingBlocks.Configuration;
using Dewiride.Erp.BuildingBlocks.Endpoints;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.BuildingBlocks.Observability.Health;
using Dewiride.Erp.BuildingBlocks.Observability.Telemetry;
using Dewiride.Erp.Host.Api;
using Dewiride.Erp.Host.Api.Pipeline;

var builder = WebApplication.CreateBuilder(args);

builder.AddErpConfiguration(typeof(Program).Assembly);
builder.AddErpTelemetry();
builder.AddErpHealthChecks();
builder.AddErpEndpoints();
builder.AddRequestPipeline();
builder.AddModules(Modules.All);

var app = builder.Build();

app.UseRequestPipeline();
app.MapErpHealthChecks();
app.MapErpOpenApi();
app.MapModules();

app.Run();

public partial class Program;
