using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Platform.SystemInfo.Persistence;
using Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Domain;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Application.Commands.RecordStartup;

internal sealed class RecordStartupHandler(SystemInfoDbContext context, TimeProvider timeProvider) : ICommandHandler<RecordStartupCommand, ApiStartupId>
{
    public Task<Result<ApiStartupId>> HandleAsync(RecordStartupCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var startup = ApiStartup.Record(
            command.ApplicationName,
            command.Build,
            command.EnvironmentName,
            command.ConfigurationLabel,
            command.MachineName,
            command.StartedAt,
            timeProvider.GetUtcNow());
        if (startup.IsFailure)
        {
            return Task.FromResult<Result<ApiStartupId>>(startup.Error!);
        }

        context.Startups.Add(startup.Value);

        return Task.FromResult<Result<ApiStartupId>>(startup.Value.Id);
    }
}
