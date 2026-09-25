using Dewiride.Erp.BuildingBlocks.Endpoints;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.BuildingBlocks.Observability.Health;
using Dewiride.Erp.BuildingBlocks.Observability.Telemetry;
using Dewiride.Erp.Host.Api.OpenApi;
using Dewiride.Erp.Host.Api.Pipeline;
using Dewiride.Erp.Host.Composition;

var builder = WebApplication.CreateBuilder(args);

builder.AddErpPlatform(typeof(Program).Assembly);
builder.AddErpTelemetry();
builder.AddErpHealthChecks();
builder.AddErpEndpoints();
builder.AddErpOpenApi();
builder.AddRequestPipeline();

var app = builder.Build();

app.UseRequestPipeline();
app.MapErpHealthChecks();
app.MapErpOpenApi();
app.MapModules();

app.Run();

public partial class Program;
