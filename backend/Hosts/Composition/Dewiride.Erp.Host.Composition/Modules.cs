using Dewiride.Erp.BuildingBlocks.Modules;
using ErpModules = Dewiride.Erp.Modules;

namespace Dewiride.Erp.Host.Composition;

public static class Modules
{
    public static IReadOnlyList<IModule> All { get; } =
    [
        new ErpModules.Platform.SystemInfo.SystemInfoModule(),
        new ErpModules.Platform.Attachments.AttachmentsModule(),
        new ErpModules.Platform.Audit.AuditModule(),
        new ErpModules.Platform.Jobs.JobsModule(),
        new ErpModules.Identity.Users.UsersModule(),
        new ErpModules.Platform.ReferenceData.ReferenceDataModule(),
        new ErpModules.Compliance.StatutoryParameters.StatutoryParametersModule(),
    ];
}
