using Dewiride.Erp.BuildingBlocks.Attachments.Encryption;
using Dewiride.Erp.BuildingBlocks.Attachments.Hosting;
using Dewiride.Erp.BuildingBlocks.Attachments.Options;
using Dewiride.Erp.BuildingBlocks.Attachments.Persistence;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.BuildingBlocks.Attachments.Storage;
using Dewiride.Erp.BuildingBlocks.Attachments.Storage.Blob;
using Dewiride.Erp.BuildingBlocks.Attachments.Telemetry;
using Dewiride.Erp.BuildingBlocks.Observability.Health;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;

namespace Dewiride.Erp.BuildingBlocks.Attachments;

public static class AttachmentsRegistration
{
    public const string BlobActivitySources = "Azure.Storage.Blobs.*";

    public static IHostApplicationBuilder AddErpAttachments(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // No ValidateOnStart: AttachmentStorageInitializer validates when the API starts, and the migrator, which composes
        // these services without starting its host, needs neither the storage endpoint nor the encryption key.
        builder.Services.AddOptions<AttachmentsOptions>()
            .BindConfiguration(AttachmentsOptions.SectionName)
            .ValidateDataAnnotations();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<AttachmentsOptions>, AttachmentsOptionsValidator>());
        builder.AddModuleDbContext<AttachmentsDbContext>(AttachmentsDbContext.SchemaName);
        builder.Services.TryAddSingleton<KeyRing>();
        builder.Services.TryAddSingleton(AttachmentBlobClients.Create);
        builder.Services.TryAddSingleton<IDocumentStore, BlobDocumentStore>();
        builder.Services.TryAddSingleton<AttachmentsMetrics>();
        builder.Services.TryAddScoped<AttachmentUploader>();
        builder.Services.TryAddScoped<IAttachmentService, AttachmentService>();
        builder.Services.AddHostedService<AttachmentStorageInitializer>();
        builder.Services.AddHostedService<UploadReservationSweeper>();
        builder.Services.AddHealthChecks()
            .AddCheck<AttachmentStorageHealthCheck>(AttachmentStorageHealthCheck.Name, HealthStatus.Degraded, [HealthEndpoints.ReadyTag]);
        builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddSource(BlobActivitySources));

        return builder;
    }
}
