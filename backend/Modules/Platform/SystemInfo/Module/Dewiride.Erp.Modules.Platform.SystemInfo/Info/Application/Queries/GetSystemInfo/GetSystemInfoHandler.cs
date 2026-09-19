using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.Info.Application.Queries.GetSystemInfo;

internal sealed class GetSystemInfoHandler(ApplicationInfo application, TimeProvider timeProvider) : IQueryHandler<GetSystemInfoQuery, SystemInfoDetails>
{
    public Task<Result<SystemInfoDetails>> HandleAsync(GetSystemInfoQuery query, CancellationToken cancellationToken)
    {
        var uptime = timeProvider.GetUtcNow() - application.StartedAt;
        var details = new SystemInfoDetails(application.Name, application.Version, application.StartedAt, uptime);
        return Task.FromResult(Result.Success(details));
    }
}
