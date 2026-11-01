using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;

namespace Dewiride.Erp.Modules.Platform.Jobs.Runs.Application.Commands.RerunJobRun;

internal sealed record RerunJobRunCommand(JobRunId Id) : ICommand<JobRunDetails>;
