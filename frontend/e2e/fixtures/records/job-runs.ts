import { expect, type APIRequestContext } from "@playwright/test";
import { randomUUID } from "node:crypto";

export const jobRunsSeedPath = "/api/__test/jobs/runs";

export const jobSchedulesSeedPath = "/api/__test/jobs/schedules";

export const accountCheckJob = "identity.users.directory-account-check";

export const retiredJob = "e2e.jobs.retired";

export const scheduledTestJob = "test.jobs.end-to-end-scheduled";

export const misscheduledTestJob = "test.jobs.end-to-end-misscheduled";

export type JobRunStatus = "pending" | "running" | "failed" | "succeeded" | "deadLettered" | "cancelled";

export type JobRunSeed = {
  readonly jobName: string;
  readonly idempotencyKey: string;
  readonly status: JobRunStatus;
  readonly attempts?: number;
  readonly errorCode?: string;
  readonly errorMessage?: string;
  readonly stopRequested?: boolean;
  readonly requestedBy?: string;
};

export type JobScheduleSeed = {
  readonly jobName: typeof scheduledTestJob | typeof misscheduledTestJob;
  readonly lastRunId: string;
  readonly lastOccurrenceAt: string;
};

// The end-to-end API host runs no job runner, so no run ever runs there, and a spec shows a run in each state by seeding it
// with the history the ERP would have recorded on the way, one second a step and ending now. A run may belong to a job no
// version of the ERP has (retiredJob), which shows how the pages treat a run of a job that is gone. The request carries no
// session: the route is the test host's own and takes no antiforgery token.
export async function seedJobRun(request: APIRequestContext, seed: JobRunSeed): Promise<string> {
  const response = await request.post(jobRunsSeedPath, { data: seed });
  expect(response.status(), `seeding the ${seed.status} run ${seed.idempotencyKey}`).toBe(200);
  const { id } = (await response.json()) as { id?: unknown };
  expect(typeof id, `the id of the run ${seed.idempotencyKey}`).toBe("string");
  return id as string;
}

// Only the host's two test jobs take a last run, and a job has one schedule row whose first seed stays for the life of the
// host, so every project's seed of a job answers that first one and a spec reads the last run from the answer. Every spec
// seeds the scheduled job with a run seeded for it and the misscheduled one with an id no run has, so whichever seeds first,
// the pages show one job with its last run and one whose last run has no record.
export async function seedJobSchedule(
  request: APIRequestContext,
  seed: JobScheduleSeed,
): Promise<JobScheduleSeed> {
  const response = await request.post(jobSchedulesSeedPath, { data: seed });
  expect(response.status(), `seeding the last run of ${seed.jobName}`).toBe(200);
  return (await response.json()) as JobScheduleSeed;
}

// Every spec shares the host's database, so each one keys the runs it seeds with a prefix of its own and filters the list by
// it, never counting anyone else's runs.
export function runKeyPrefix(scenario: string): string {
  return `e2e-${scenario}-${randomUUID().slice(-12)}`;
}
