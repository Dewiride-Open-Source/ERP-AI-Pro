namespace Dewiride.Erp.Modules.Platform.Jobs.Runs.Endpoints.Responses;

/// <summary>A job the ERP runs on its own, on a schedule or whenever a module asks for a run.</summary>
/// <param name="Name">Name of the job, three lower-case segments such as identity.users.directory-account-check, which each of its runs carries.</param>
/// <param name="Title">Short title of the job.</param>
/// <param name="Description">One sentence saying what the job does.</param>
/// <param name="MaxAttempts">Attempts a run of the job makes before it gives up, counted from its enqueue or its last re-run.</param>
/// <param name="Schedule">When the job runs on its own; null for a job that runs only when a module asks for a run.</param>
internal sealed record JobResponse(
    string Name,
    string Title,
    string Description,
    int MaxAttempts,
    JobScheduleResponse? Schedule);
