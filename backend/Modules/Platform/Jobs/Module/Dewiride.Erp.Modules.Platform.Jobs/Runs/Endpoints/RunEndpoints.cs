using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.Modules.Platform.Jobs.Contracts;
using Dewiride.Erp.Modules.Platform.Jobs.Runs.Application.Commands.CancelJobRun;
using Dewiride.Erp.Modules.Platform.Jobs.Runs.Application.Commands.RerunJobRun;
using Dewiride.Erp.Modules.Platform.Jobs.Runs.Application.Queries.GetJobRun;
using Dewiride.Erp.Modules.Platform.Jobs.Runs.Application.Queries.ListJobRuns;
using Dewiride.Erp.Modules.Platform.Jobs.Runs.Application.Queries.ListJobs;
using Dewiride.Erp.Modules.Platform.Jobs.Runs.Endpoints.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Platform.Jobs.Runs.Endpoints;

// A re-run and a cancel change a run that exists, so neither takes an idempotency key: a repeated re-run finds the run waiting
// and is refused, and a repeated cancel finds it cancelled and is refused, or finds it running and asked to stop already and
// answers it as it stands.
internal static class RunEndpoints
{
    public const string ListJobsRouteName = "Platform.Jobs.ListJobs";

    public const string ListRunsRouteName = "Platform.Jobs.ListJobRuns";

    public const string GetRunRouteName = "Platform.Jobs.GetJobRun";

    public const string RerunRouteName = "Platform.Jobs.RerunJobRun";

    public const string CancelRouteName = "Platform.Jobs.CancelJobRun";

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", ListJobsAsync)
            .WithName(ListJobsRouteName)
            .WithSummary("Lists the jobs the ERP runs on its own, by name, with the schedule, the next occurrence and the latest run of each recurring job.")
            .RequirePermission(JobsPermissions.Read);

        group.MapGet("/runs", ListRunsAsync)
            .WithName(ListRunsRouteName)
            .WithSummary("Lists the runs of the jobs, newest first unless the caller sorts them, with the name each person who asked for one has today.")
            .RequirePermission(JobsPermissions.Read);

        group.MapGet("/runs/{id:guid}", GetRunAsync)
            .WithName(GetRunRouteName)
            .WithSummary("Describes one job run with every step of its history, oldest first: its attempts and their outcomes, and who re-ran or cancelled it.")
            .RequirePermission(JobsPermissions.Read);

        group.MapPost("/runs/{id:guid}/rerun", RerunAsync)
            .WithName(RerunRouteName)
            .WithSummary("Puts a run that failed, gave up or was cancelled back to run again at once, with its failures counted from zero.")
            .RequirePermission(JobsPermissions.Rerun)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/runs/{id:guid}/cancel", CancelAsync)
            .WithName(CancelRouteName)
            .WithSummary("Cancels a run that waits to start or to try again, or asks a running run to stop, which its runner sees at its next lease renewal; asking a running run again changes nothing.")
            .RequirePermission(JobsPermissions.Cancel)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static JobResponse ToResponse(JobDetails details) =>
        new(details.Name, details.Title, details.Description, details.MaxAttempts, details.Schedule is { } schedule ? ToResponse(schedule) : null);

    private static JobScheduleResponse ToResponse(JobScheduleDetails details) =>
        new(details.Expression, details.NextOccurrenceAt, details.Problem, details.LastOccurrenceAt, details.LastRunId?.Value, details.LastRunStatus);

    private static JobRunResponse ToResponse(JobRunDetails details) =>
        new(
            details.Id.Value,
            details.JobName,
            details.JobTitle,
            details.IdempotencyKey,
            details.Status,
            details.DueAt,
            details.CreatedAt,
            ToResponse(details.RequestedBy),
            details.AttemptCount,
            details.FailureCount,
            details.MaxAttempts,
            details.StartedAt,
            details.FinishedAt,
            details.CancelRequestedAt,
            details.LastErrorCode,
            details.LastErrorMessage);

    private static JobRunEventResponse ToResponse(JobRunEventDetails details) =>
        new(
            details.Id,
            details.OccurredAt,
            details.Kind,
            ToResponse(details.Actor),
            details.Attempt,
            details.Runner,
            details.TraceId,
            details.ErrorCode,
            details.ErrorMessage,
            details.RetryAt);

    private static JobActorResponse ToResponse(JobActorDetails details) => new(details.Kind, details.Id, details.Name);

    private static async Task<Results<Ok<IReadOnlyList<JobResponse>>, ProblemHttpResult>> ListJobsAsync(
        IQueryHandler<ListJobsQuery, IReadOnlyList<JobDetails>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListJobsQuery(), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error!.ToProblem();
        }

        IReadOnlyList<JobResponse> jobs = [.. result.Value.Select(ToResponse)];

        return TypedResults.Ok(jobs);
    }

    private static async Task<Results<Ok<PagedResponse<JobRunResponse>>, ProblemHttpResult>> ListRunsAsync(
        [AsParameters] PagingParameters paging,
        IQueryHandler<ListJobRunsQuery, PagedResult<JobRunDetails>> handler,
        CancellationToken cancellationToken)
    {
        var request = paging.ToListRequest();
        if (request.IsFailure)
        {
            return request.Error!.ToProblem();
        }

        var result = await handler.HandleAsync(new ListJobRunsQuery(request.Value), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value.ToResponse(ToResponse)) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<JobRunHistoryResponse>, ProblemHttpResult>> GetRunAsync(
        Guid id,
        IQueryHandler<GetJobRunQuery, JobRunHistory> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetJobRunQuery(JobRunId.From(id)), cancellationToken);

        return result.IsSuccess
            ? TypedResults.Ok(new JobRunHistoryResponse(ToResponse(result.Value.Run), [.. result.Value.Events.Select(ToResponse)]))
            : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<JobRunResponse>, ProblemHttpResult>> RerunAsync(
        Guid id,
        ICommandHandler<RerunJobRunCommand, JobRunDetails> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new RerunJobRunCommand(JobRunId.From(id)), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(ToResponse(result.Value)) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<JobRunResponse>, ProblemHttpResult>> CancelAsync(
        Guid id,
        ICommandHandler<CancelJobRunCommand, JobRunDetails> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new CancelJobRunCommand(JobRunId.From(id)), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(ToResponse(result.Value)) : result.Error!.ToProblem();
    }
}
