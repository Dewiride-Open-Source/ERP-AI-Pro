using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;

namespace Dewiride.Erp.Modules.Platform.Jobs.Runs.Application.Commands.CancelJobRun;

internal sealed record CancelJobRunCommand(JobRunId Id) : ICommand<JobRunDetails>;
