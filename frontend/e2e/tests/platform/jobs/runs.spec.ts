import type { APIRequestContext } from "@playwright/test";
import { randomUUID } from "node:crypto";

import {
  accountCheckJob,
  misscheduledTestJob,
  retiredJob,
  runKeyPrefix,
  scheduledTestJob,
  seedJobRun,
  seedJobSchedule,
  type JobRunSeed,
  type JobRunStatus,
} from "../../../fixtures/job-runs";
import { holdServerFunctionCalls } from "../../../fixtures/server-functions";
import { expect, forEachTheme, test, themes, type SignedInApi } from "../../../fixtures/test";
import { JobRunPage } from "../../../pages/platform/jobs/run.page";
import {
  jobRunApi,
  jobRunPath,
  jobRunsApi,
  jobsApi,
  JobsPage,
  jobsPath,
} from "../../../pages/platform/jobs/runs.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

type ApiSchedule = {
  readonly expression: string;
  readonly nextOccurrenceAt: string | null;
  readonly problem: string | null;
  readonly lastOccurrenceAt: string | null;
  readonly lastRunId: string | null;
  readonly lastRunStatus: JobRunStatus | null;
};

type ApiJob = {
  readonly name: string;
  readonly title: string;
  readonly description: string;
  readonly maxAttempts: number;
  readonly schedule: ApiSchedule | null;
};

type ApiRun = {
  readonly id: string;
  readonly idempotencyKey: string;
  readonly status: JobRunStatus;
  readonly createdAt: string;
  readonly dueAt: string;
  readonly failureCount: number;
};

type SeededRun = { readonly id: string; readonly key: string };

const pageSize = 10;

const createdSort = "Sorted by Created, latest first.";

const keyProblem = "A key holds only letters, digits and the characters . _ : + -, without spaces.";

const finishedMeanwhile =
  "This run has finished meanwhile, so there is nothing to cancel. Reload the page to see how it ended.";

const notRerunnable =
  "This run is waiting, running or done by now, so it cannot run again. Reload the page to see where it stands.";

const stillRunning =
  "This run is still running, so it cannot be cancelled yet. Try again in a few minutes: once it has stopped answering, the ERP cancels it at once.";

const notHere =
  "If it has stopped answering, it is cancelled at once; otherwise it stops only once the version of the ERP that started it notices the request.";

const indiaDateTime = new Intl.DateTimeFormat("en-IN", {
  timeZone: "Asia/Kolkata",
  dateStyle: "medium",
  timeStyle: "short",
});

const indiaTime = new Intl.DateTimeFormat("en-IN", {
  timeZone: "Asia/Kolkata",
  hour: "numeric",
  minute: "2-digit",
});

const indiaDate = new Intl.DateTimeFormat("en-CA", {
  timeZone: "Asia/Kolkata",
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
});

// Eleven runs, one more than the first page holds, in every status, of the account check and of a job no version of the ERP
// has; five are the account check's and one of those gave up.
const listSeeds: readonly Omit<JobRunSeed, "idempotencyKey">[] = [
  { jobName: accountCheckJob, status: "succeeded" },
  { jobName: retiredJob, status: "pending" },
  { jobName: accountCheckJob, status: "failed", attempts: 2 },
  { jobName: retiredJob, status: "running" },
  { jobName: accountCheckJob, status: "deadLettered", attempts: 6 },
  { jobName: retiredJob, status: "cancelled" },
  { jobName: accountCheckJob, status: "succeeded", attempts: 3 },
  { jobName: retiredJob, status: "pending" },
  { jobName: retiredJob, status: "failed" },
  { jobName: accountCheckJob, status: "running", stopRequested: true },
  { jobName: retiredJob, status: "pending" },
];

// An accessible name holding ": " reads as a YAML mapping inside a snapshot, so such a node is written as a quoted YAML string.
function quotedNode(node: string): string {
  return `'${node.replaceAll("'", "''")}'`;
}

async function seedRuns(
  request: APIRequestContext,
  prefix: string,
  seeds: readonly Omit<JobRunSeed, "idempotencyKey">[],
): Promise<SeededRun[]> {
  const runs: SeededRun[] = [];
  for (const [index, seed] of seeds.entries()) {
    const key = `${prefix}-${String(index + 1).padStart(2, "0")}`;
    runs.push({ id: await seedJobRun(request, { ...seed, idempotencyKey: key }), key });
  }
  return runs;
}

async function jobOf(api: SignedInApi, name: string): Promise<ApiJob> {
  const response = await api.get(jobsApi);
  expect(response.status(), "the jobs").toBe(200);
  const job = ((await response.json()) as ApiJob[]).find((candidate) => candidate.name === name);
  expect(job, `${name} among the jobs`).toBeDefined();
  return job!;
}

function accountCheckOf(api: SignedInApi): Promise<ApiJob> {
  return jobOf(api, accountCheckJob);
}

function scheduleOf(job: ApiJob): ApiSchedule {
  expect(job.schedule, `the schedule of ${job.name}`).not.toBeNull();
  return job.schedule!;
}

async function runsOf(api: SignedInApi, prefix: string, sort: string): Promise<ApiRun[]> {
  const query = new URLSearchParams({ filter: `idempotencyKey:contains:${prefix}`, sort, pageSize: "100" });
  const response = await api.get(`${jobRunsApi}?${query}`);
  expect(response.status(), "the runs the test seeded").toBe(200);
  return ((await response.json()) as { items?: ApiRun[] }).items ?? [];
}

async function firstKeys(api: SignedInApi, prefix: string, sort: string): Promise<string[]> {
  return (await runsOf(api, prefix, sort)).slice(0, pageSize).map((run) => run.idempotencyKey);
}

function istDateTime(instant: string): string {
  return indiaDateTime.format(new Date(instant));
}

function runName(run: ApiRun): string {
  return `created ${istDateTime(run.createdAt)}`;
}

async function runOf(api: SignedInApi, id: string): Promise<ApiRun> {
  const response = await api.get(jobRunApi(id));
  expect(response.status(), "the run").toBe(200);
  return ((await response.json()) as { run: ApiRun }).run;
}

function isoIndiaDate(instant: string): string {
  return indiaDate.format(new Date(instant));
}

function dayBefore(isoDate: string): string {
  const day = new Date(`${isoDate}T00:00:00Z`);
  day.setUTCDate(day.getUTCDate() - 1);
  return day.toISOString().slice(0, 10);
}

function typedDate(isoDate: string): string {
  const [year, month, day] = isoDate.split("-");
  return `${day}-${month}-${year}`;
}

test.describe("background jobs", () => {
  test.use({ persona: "administrator" });

  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("shows each job with its schedule in India time, its next run and its tries", async ({
        page,
        api,
        capture,
      }) => {
        const jobsPage = new JobsPage(page);
        const accountCheck = await accountCheckOf(api);
        expect(accountCheck.schedule?.expression, "the account check's schedule").toBe("30 0 * * *");
        const nextRun = accountCheck.schedule?.nextOccurrenceAt ?? "";
        expect(indiaTime.format(new Date(nextRun)), "the account check's next run in India time").toBe(
          "12:30 am",
        );

        await jobsPage.goto();

        await expect(page).toHaveTitle(/Background jobs · ERP-AI-Pro/);
        await expect(jobsPage.jobsUnavailable).toHaveCount(0);
        const job = jobsPage.job(accountCheck.title);
        await expect(job.getByTestId("job-problem")).toHaveCount(0);
        await expect(job).toMatchAriaSnapshot(`
          - listitem:
            - heading "${accountCheck.title}" [level=3]
            - paragraph: ${JSON.stringify(accountCheck.description)}
            - term: Schedule
            - definition: "30 0 * * * (India time)"
            - term: Next run
            - definition: ${JSON.stringify(istDateTime(nextRun))}
            - term: Last run
            - definition: Not run yet
            - term: Tries
            - definition: Up to ${accountCheck.maxAttempts} times
        `);
        await capture("jobs-card", jobsPage.jobsCard);
      });

      test("shows a job's last run with its status and opens it", async ({ page, api, request, capture }) => {
        const jobsPage = new JobsPage(page);
        const runPage = new JobRunPage(page);
        const runId = await seedJobRun(request, {
          jobName: scheduledTestJob,
          idempotencyKey: `${runKeyPrefix("last-run")}-done`,
          status: "succeeded",
        });
        const seeded = await seedJobSchedule(request, {
          jobName: scheduledTestJob,
          lastRunId: runId,
          lastOccurrenceAt: new Date().toISOString(),
        });
        const scheduled = await jobOf(api, scheduledTestJob);
        const schedule = scheduleOf(scheduled);
        expect(schedule, "the scheduled test job's schedule").toMatchObject({
          expression: "15 2 * * *",
          nextOccurrenceAt: expect.any(String),
          problem: null,
          lastRunId: seeded.lastRunId,
          lastRunStatus: "succeeded",
        });
        const job = jobsPage.job(scheduled.title);

        await jobsPage.goto();

        await expect(job.getByTestId("job-problem")).toHaveCount(0);
        await expect(job).toMatchAriaSnapshot(`
          - listitem:
            - heading "${scheduled.title}" [level=3]
            - paragraph: ${JSON.stringify(scheduled.description)}
            - term: Schedule
            - definition: "15 2 * * * (India time)"
            - term: Next run
            - definition: ${JSON.stringify(istDateTime(schedule.nextOccurrenceAt ?? ""))}
            - term: Last run
            - definition:
              - text: ${JSON.stringify(`${istDateTime(seeded.lastOccurrenceAt)} Done`)}
              - link "Open the last run of ${scheduled.title}":
                - /url: ${jobRunPath(seeded.lastRunId)}
            - term: Tries
            - definition: Up to ${scheduled.maxAttempts} times
        `);
        await capture("jobs-last-run", job);

        await job.getByRole("link", { name: `Open the last run of ${scheduled.title}` }).click();
        await expect(page).toHaveURL((url) => url.pathname === jobRunPath(seeded.lastRunId));
        await expect(runPage.heading).toHaveText(scheduled.title);
        await expect(runPage.statusLine).toHaveText("Done");
      });

      test("shows a job whose schedule cannot run beside a last run that is no longer recorded", async ({
        page,
        api,
        request,
        capture,
      }) => {
        const jobsPage = new JobsPage(page);
        const seeded = await seedJobSchedule(request, {
          jobName: misscheduledTestJob,
          lastRunId: randomUUID(),
          lastOccurrenceAt: new Date().toISOString(),
        });
        const misscheduled = await jobOf(api, misscheduledTestJob);
        expect(scheduleOf(misscheduled), "the misscheduled test job's schedule").toMatchObject({
          expression: "61 * * * *",
          nextOccurrenceAt: null,
          lastRunId: seeded.lastRunId,
          lastRunStatus: null,
        });
        const job = jobsPage.job(misscheduled.title);

        await jobsPage.goto();

        await expect(job).toMatchAriaSnapshot(`
          - listitem:
            - heading "${misscheduled.title}" [level=3]
            - paragraph: ${JSON.stringify(misscheduled.description)}
            - term: Schedule
            - definition: "61 * * * * (India time)"
            - term: Next run
            - definition: Not scheduled
            - term: Last run
            - definition: ${JSON.stringify(istDateTime(seeded.lastOccurrenceAt))}
            - term: Tries
            - definition: Up to ${misscheduled.maxAttempts} times
            - alert:
              - text: The ERP does not run this job on its own now
              - paragraph: "The minute field holds 61, but the minute allows 0 to 59."
              - paragraph: Once its schedule setting is corrected, the ERP runs it on its own again.
        `);
        await expect(job.getByRole("link"), "a last run that is no longer recorded").toHaveCount(0);
        await capture("jobs-schedule-problem", job);
      });

      test("lists the runs and filters, sorts, pages and hides their columns", async ({
        page,
        api,
        request,
        capture,
      }, testInfo) => {
        test.slow();
        const jobsPage = new JobsPage(page);
        const { list } = jobsPage;
        const prefix = runKeyPrefix("runs");
        const accountCheck = await accountCheckOf(api);
        const runs = await seedRuns(request, prefix, listSeeds);
        const seeded = await runsOf(api, prefix, "createdAt:desc");
        expect(seeded, "the runs the test seeded").toHaveLength(listSeeds.length);
        const byKey = `?key=${prefix}`;

        await jobsPage.goto();
        await jobsPage.waitUntilInteractive();
        await expect(list.filters).toMatchAriaSnapshot(`
          - search "Filter runs":
            - group:
              - text: Job
              - button "Job Any"
            - group:
              - text: Status
              - button "Status Any"
            - group:
              - text: Key
              - group:
                - group
                - searchbox "Key"
            - group:
              - text: Created between From
              - group:
                - textbox "Created between From"
              - text: To
              - group:
                - textbox "Created between To"
              - button "Choose dates"
            - button "Apply filters"
        `);

        const keyFilter = list.textFilter("Key");
        await keyFilter.fill(prefix);
        await keyFilter.press("Enter");
        await expect(page).toHaveURL((url) => url.search === byKey);
        await expect(keyFilter).toBeFocused();
        await expect(list.status).toHaveText(`Showing 1–${pageSize} of 11 runs. ${createdSort}`);
        await expect(list.pageLabel).toHaveText("Page 1 of 2");
        expect(await jobsPage.keys()).toEqual(seeded.slice(0, pageSize).map((run) => run.idempotencyKey));
        if (testInfo.project.name.startsWith("mobile-")) expect(await list.showsCards()).toBe(true);

        const failedKey = runs[2]!.key;
        const failed = seeded.find((run) => run.idempotencyKey === failedKey)!;
        const failedRow = await jobsPage.run(failedKey);
        const failedStatus = `Failed Tries again at ${istDateTime(failed.dueAt)}`;
        const failedName = `${accountCheck.title}, ${runName(failed)}`;
        if (await list.showsCards()) {
          await expect(failedRow).toMatchAriaSnapshot(`
            - listitem:
              - link ${JSON.stringify(failedName)}:
                - /url: ${jobRunPath(failed.id)}
              - term: Status
              - definition: ${JSON.stringify(failedStatus)}
              - term: Key
              - definition: ${failedKey}
              - term: Created
              - definition: ${JSON.stringify(istDateTime(failed.createdAt))}
              - term: Due
              - definition: ${JSON.stringify(istDateTime(failed.dueAt))}
              - term: Finished
              - definition: Not finished
              - term: Tries
              - definition: 2 tries, 2 failed
              - term: Last error
              - definition: The attempt failed.
              - ${quotedNode(`button "Run again: ${failedName}"`)}
              - ${quotedNode(`button "Cancel: ${failedName}"`)}
          `);
        } else {
          await expect(list.columnHeader("Created")).toHaveAttribute("aria-sort", "descending");
          await expect(failedRow).toMatchAriaSnapshot(`
            - row:
              - rowheader ${JSON.stringify(failedName)}:
                - link ${JSON.stringify(failedName)}:
                  - /url: ${jobRunPath(failed.id)}
              - cell ${JSON.stringify(failedStatus)}
              - cell "${failedKey}"
              - cell ${JSON.stringify(istDateTime(failed.createdAt))}
              - cell ${JSON.stringify(istDateTime(failed.dueAt))}
              - cell "Not finished"
              - cell "2 tries, 2 failed"
              - cell "The attempt failed."
              - cell:
                - ${quotedNode(`button "Run again: ${failedName}"`)}
                - ${quotedNode(`button "Cancel: ${failedName}"`)}
          `);
        }
        const waiting = await jobsPage.run(runs[1]!.key);
        await expect(jobsPage.status(waiting)).toHaveText("Waiting");
        await expect(jobsPage.rerun(waiting)).toHaveCount(0);
        await expect(jobsPage.status(await jobsPage.run(runs[9]!.key))).toHaveText("Running Stop requested");
        await expect(jobsPage.cancel(await jobsPage.run(runs[9]!.key))).toHaveCount(0);
        await expect(jobsPage.rerun(await jobsPage.run(runs[5]!.key))).toHaveCount(0);
        await capture("job-runs", jobsPage.runsCard);

        await list.nextPage.click();
        await expect(page).toHaveURL((url) => url.searchParams.get("page") === "2");
        await expect(list.pageLabel).toHaveText("Page 2 of 2");
        await expect(list.status).toHaveText(`Showing 11–11 of 11 runs. ${createdSort}`);
        expect(await jobsPage.keys()).toEqual([seeded[pageSize]!.idempotencyKey]);
        await list.previousPage.click();
        await expect(page).toHaveURL((url) => url.search === byKey);
        await expect(list.pageLabel).toHaveText("Page 1 of 2");

        await list.choosePageSize(20);
        await expect(page).toHaveURL((url) => url.searchParams.get("size") === "20");
        await expect(list.status).toHaveText(`Showing all 11 runs. ${createdSort}`);
        await expect(list.nextPage).toHaveCount(0);
        await list.choosePageSize(pageSize);
        await expect(page).toHaveURL((url) => url.search === byKey);

        await list.sortBy("Due", "Due, latest first");
        await expect(page).toHaveURL((url) => url.searchParams.get("sort") === "dueAt:desc");
        await expect(list.status).toHaveText(
          `Showing 1–${pageSize} of 11 runs. Sorted by Due, latest first.`,
        );
        expect(await jobsPage.keys()).toEqual(await firstKeys(api, prefix, "dueAt:desc"));
        await list.sortBy("Due", "Due, earliest first");
        await expect(page).toHaveURL((url) => url.searchParams.get("sort") === "dueAt:asc");
        expect(await jobsPage.keys()).toEqual(await firstKeys(api, prefix, "dueAt:asc"));
        await list.sortBy("Finished", "Finished, latest first");
        await expect(page).toHaveURL((url) => url.searchParams.get("sort") === "finishedAt:desc");
        expect(await jobsPage.keys()).toEqual(await firstKeys(api, prefix, "finishedAt:desc"));
        await list.sortBy("Created", "Created, latest first");
        await expect(page).toHaveURL((url) => url.search === byKey);
        await expect(list.status).toHaveText(`Showing 1–${pageSize} of 11 runs. ${createdSort}`);

        await list.chooseOptions("Job", [accountCheck.title]);
        await list.applyFilters.click();
        await expect(page).toHaveURL((url) => url.searchParams.getAll("job").join() === accountCheckJob);
        await expect(list.status).toHaveText(`Showing all 5 runs. ${createdSort}`);
        await list.chooseOptions("Status", ["Gave up"]);
        await list.applyFilters.click();
        await expect(page).toHaveURL((url) => url.searchParams.getAll("status").join() === "deadLettered");
        await expect(list.status).toHaveText("Showing 1 run.");
        expect(await jobsPage.keys()).toEqual([runs[4]!.key]);
        await expect(page).toHaveTitle(/Background jobs · ERP-AI-Pro/);
        await capture("job-runs-filtered", jobsPage.runsCard);
        for (const filter of ["Job", "Status"]) {
          await list.optionsFilter(filter).click();
          await page.getByRole("menuitem", { name: "Clear choices" }).click();
          await expect(page.getByRole("menu")).toHaveCount(0);
        }
        await list.applyFilters.click();
        await expect(page).toHaveURL((url) => url.search === byKey);
        await expect(list.status).toHaveText(`Showing 1–${pageSize} of 11 runs. ${createdSort}`);

        await keyFilter.fill(`${prefix} 01`);
        await list.applyFilters.click();
        await expect(keyFilter).toBeFocused();
        await expect(keyFilter).toHaveAttribute("aria-invalid", "true");
        await expect(list.filters.getByText(keyProblem)).toBeVisible();
        await expect(page).toHaveURL((url) => url.search === byKey);
        await capture("job-runs-invalid-key", list.filters);
        await keyFilter.fill(prefix);
        await expect(list.filters.getByText(keyProblem)).toHaveCount(0);

        const createdOn = isoIndiaDate(seeded[0]!.createdAt);
        test.skip(
          seeded.some((run) => isoIndiaDate(run.createdAt) !== createdOn),
          "The seeded runs straddle midnight in India, so they fall on two days.",
        );
        await list.rangeStart("Created between").fill(typedDate(createdOn));
        await list.rangeEnd("Created between").fill(typedDate(createdOn));
        await list.applyFilters.click();
        await expect(page).toHaveURL(
          (url) =>
            url.searchParams.get("createdFrom") === createdOn &&
            url.searchParams.get("createdTo") === createdOn,
        );
        await expect(list.status).toHaveText(`Showing 1–${pageSize} of 11 runs. ${createdSort}`);
        await list.rangeStart("Created between").fill(typedDate(dayBefore(createdOn)));
        await list.rangeEnd("Created between").fill(typedDate(dayBefore(createdOn)));
        await list.applyFilters.click();
        await expect(list.noMatches).toContainText("No runs match these filters.");
        await expect(list.status).toHaveText("No runs to show.");
        await expect(page).toHaveTitle(/Background jobs · ERP-AI-Pro/);
        await capture("job-runs-no-matches", jobsPage.runsCard);
        await list.noMatches.getByRole("link", { name: "Clear filters" }).click();
        await expect(page).toHaveURL((url) => url.pathname === jobsPath && url.search === "");
        await expect(list.textFilter("Key")).toHaveValue("");

        await jobsPage.goto(byKey);
        await jobsPage.waitUntilInteractive();
        await list.chooseOptions("Status", ["Waiting"]);
        await list.applyFilters.click();
        await expect(list.status).toHaveText(`Showing all 3 runs. ${createdSort}`);
        await list.filters.getByRole("link", { name: "Clear filters" }).click();
        await expect(page).toHaveURL((url) => url.pathname === jobsPath && url.search === "");
        await expect(list.optionsFilter("Job")).toBeFocused();
        await page.goBack();
        await expect(page).toHaveURL((url) => url.searchParams.getAll("status").join() === "pending");
        await expect(list.status).toHaveText(`Showing all 3 runs. ${createdSort}`);

        await jobsPage.goto(byKey);
        await jobsPage.waitUntilInteractive();
        await list.setColumnVisible("Key", false);
        await expect(page).toHaveURL((url) => url.searchParams.get("hide") === "idempotencyKey");
        if (!(await list.showsCards())) await expect(list.columnHeader("Key")).toHaveCount(0);
        await expect((await list.rows()).getByTestId("job-run-key")).toHaveCount(0);
        await list.setColumnVisible("Key", true);
        await expect(page).toHaveURL((url) => url.search === byKey);
        expect(await jobsPage.keys()).toEqual(seeded.slice(0, pageSize).map((run) => run.idempotencyKey));
      });

      test("runs a run that gave up again once confirmed, and keeps it as it is when asked to", async ({
        page,
        api,
        request,
        capture,
      }) => {
        const jobsPage = new JobsPage(page);
        const shell = new AppShell(page);
        const accountCheck = await accountCheckOf(api);
        const prefix = runKeyPrefix("rerun");
        const key = `${prefix}-gave-up`;
        const id = await seedJobRun(request, {
          jobName: accountCheckJob,
          idempotencyKey: key,
          status: "deadLettered",
          attempts: accountCheck.maxAttempts,
        });
        const name = runName(await runOf(api, id));

        await jobsPage.goto(`?key=${prefix}`);
        await jobsPage.waitUntilInteractive();
        const run = await jobsPage.run(key);
        await expect(jobsPage.status(run)).toHaveText("Gave up");
        await expect(jobsPage.cancel(run)).toHaveCount(0);
        await expect(jobsPage.link(run)).toHaveAccessibleName(`${accountCheck.title}, ${name}`);
        const rerun = jobsPage.rerun(run);
        await expect(rerun).toHaveAccessibleName(`Run again: ${accountCheck.title}, ${name}`);

        await rerun.click();
        const question = jobsPage.confirmation(`Run ${accountCheck.title} again?`);
        await expect(question).toMatchAriaSnapshot(`
          - alertdialog "Run ${accountCheck.title} again?":
            - heading "Run ${accountCheck.title} again?" [level=2]
            - paragraph: ${JSON.stringify(`This run, ${name}, starts again in a moment, with all ${accountCheck.maxAttempts} of its tries available again. Its earlier tries stay in its history.`)}
            - button "Keep it as it is"
            - button "Run again"
        `);
        await capture("job-run-rerun-question", question);
        await question.getByRole("button", { name: "Keep it as it is" }).click();
        await expect(question).toHaveCount(0);
        await expect(rerun).toBeFocused();
        await expect(jobsPage.status(run)).toHaveText("Gave up");

        await rerun.click();
        await question.getByRole("button", { name: "Run again", exact: true }).click();
        await expect(shell.toast(`${accountCheck.title} will run again in a moment.`)).toBeVisible();
        await expect(jobsPage.cancel(run)).toBeFocused();
        await expect(jobsPage.status(run)).toHaveText("Waiting");
        await expect(jobsPage.rerun(run)).toHaveCount(0);
        const after = await api.get(jobRunApi(id));
        expect(after.status(), "the run after it was run again").toBe(200);
        expect(((await after.json()) as { run?: ApiRun }).run).toMatchObject({
          status: "pending",
          failureCount: 0,
        });
        await expect(page).toHaveTitle(/Background jobs · ERP-AI-Pro/);
        await capture("jobs-run-again");
      });

      test("cancels a waiting run and asks a running run to stop once confirmed, and keeps each when asked to", async ({
        page,
        api,
        request,
        capture,
      }) => {
        const jobsPage = new JobsPage(page);
        const shell = new AppShell(page);
        const accountCheck = await accountCheckOf(api);
        const prefix = runKeyPrefix("cancel");
        const waitingKey = `${prefix}-waiting`;
        const runningKey = `${prefix}-running`;
        const waitingId = await seedJobRun(request, {
          jobName: accountCheckJob,
          idempotencyKey: waitingKey,
          status: "pending",
        });
        const runningId = await seedJobRun(request, {
          jobName: accountCheckJob,
          idempotencyKey: runningKey,
          status: "running",
        });
        const waitingName = runName(await runOf(api, waitingId));
        const runningName = runName(await runOf(api, runningId));

        await jobsPage.goto(`?key=${prefix}`);
        await jobsPage.waitUntilInteractive();
        const waiting = await jobsPage.run(waitingKey);
        await expect(jobsPage.status(waiting)).toHaveText("Waiting");
        const cancelWaiting = jobsPage.cancel(waiting);
        await expect(cancelWaiting).toHaveAccessibleName(`Cancel: ${accountCheck.title}, ${waitingName}`);

        await cancelWaiting.click();
        const cancellation = jobsPage.confirmation(`Cancel this run of ${accountCheck.title}?`);
        await expect(cancellation).toMatchAriaSnapshot(`
          - alertdialog "Cancel this run of ${accountCheck.title}?":
            - heading "Cancel this run of ${accountCheck.title}?" [level=2]
            - paragraph: ${JSON.stringify(`This run, ${waitingName}, does not start. You can run it again later.`)}
            - button "Keep the run"
            - button "Cancel the run"
        `);
        await capture("job-run-cancel-question", cancellation);
        await cancellation.getByRole("button", { name: "Keep the run" }).click();
        await expect(cancellation).toHaveCount(0);
        await expect(cancelWaiting).toBeFocused();
        await expect(jobsPage.status(waiting)).toHaveText("Waiting");

        await cancelWaiting.click();
        await cancellation.getByRole("button", { name: "Cancel the run" }).click();
        await expect(shell.toast(`Cancelled the run of ${accountCheck.title}.`)).toBeVisible();
        await expect(jobsPage.rerun(waiting)).toBeFocused();
        await expect(jobsPage.status(waiting)).toHaveText("Cancelled");
        await expect(jobsPage.cancel(waiting)).toHaveCount(0);

        const running = await jobsPage.run(runningKey);
        const cancelRunning = jobsPage.cancel(running);
        await expect(jobsPage.status(running)).toHaveText("Running");
        await cancelRunning.click();
        const stop = jobsPage.confirmation(`Ask this run of ${accountCheck.title} to stop?`);
        await expect(stop).toMatchAriaSnapshot(`
          - alertdialog "Ask this run of ${accountCheck.title} to stop?":
            - heading "Ask this run of ${accountCheck.title} to stop?" [level=2]
            - paragraph: ${JSON.stringify(`This run, ${runningName}, stops within about a minute, once it notices the request. If it finishes before then, it is recorded as done.`)}
            - button "Keep it running"
            - button "Ask it to stop"
        `);
        await capture("job-run-stop-question", stop);
        await stop.getByRole("button", { name: "Keep it running" }).click();
        await expect(stop).toHaveCount(0);
        await expect(cancelRunning).toBeFocused();

        await cancelRunning.click();
        await stop.getByRole("button", { name: "Ask it to stop" }).click();
        await expect(shell.toast(`Asked the run of ${accountCheck.title} to stop.`)).toBeVisible();
        await expect(jobsPage.link(running)).toBeFocused();
        await expect(jobsPage.status(running)).toHaveText("Running Stop requested");
        await expect(jobsPage.cancel(running)).toHaveCount(0);
      });

      test("cancels a run of a job this version of the ERP does not have, and asks again to stop one that keeps running", async ({
        page,
        api,
        request,
        capture,
      }) => {
        const jobsPage = new JobsPage(page);
        const shell = new AppShell(page);
        const prefix = runKeyPrefix("cancel-retired");
        const waitingKey = `${prefix}-waiting`;
        const runningKey = `${prefix}-running`;
        const waitingId = await seedJobRun(request, {
          jobName: retiredJob,
          idempotencyKey: waitingKey,
          status: "pending",
        });
        const runningId = await seedJobRun(request, {
          jobName: retiredJob,
          idempotencyKey: runningKey,
          status: "running",
        });
        const waitingName = runName(await runOf(api, waitingId));
        const runningName = runName(await runOf(api, runningId));

        await jobsPage.goto(`?key=${prefix}`);
        await jobsPage.waitUntilInteractive();
        const waiting = await jobsPage.run(waitingKey);
        await expect(jobsPage.cancel(waiting)).toHaveAccessibleName(`Cancel: ${retiredJob}, ${waitingName}`);
        await jobsPage.cancel(waiting).click();
        const cancellation = jobsPage.confirmation(`Cancel this run of ${retiredJob}?`);
        await expect(cancellation).toMatchAriaSnapshot(`
          - alertdialog "Cancel this run of ${retiredJob}?":
            - heading "Cancel this run of ${retiredJob}?" [level=2]
            - paragraph: ${JSON.stringify(`This run, ${waitingName}, does not start. This version of the ERP does not have its job, so it cannot run it again.`)}
            - button "Keep the run"
            - button "Cancel the run"
        `);
        await cancellation.getByRole("button", { name: "Cancel the run" }).click();
        await expect(shell.toast(`Cancelled the run of ${retiredJob}.`)).toBeVisible();
        await expect(jobsPage.link(waiting)).toBeFocused();
        await expect(jobsPage.status(waiting)).toHaveText("Cancelled");
        await expect(jobsPage.cancel(waiting)).toHaveCount(0);
        await expect(jobsPage.rerun(waiting), "a run of a job the ERP no longer has").toHaveCount(0);

        const running = await jobsPage.run(runningKey);
        const cancelRunning = jobsPage.cancel(running);
        await cancelRunning.click();
        const stop = jobsPage.confirmation(`Ask this run of ${retiredJob} to stop?`);
        await expect(stop).toMatchAriaSnapshot(`
          - alertdialog "Ask this run of ${retiredJob} to stop?":
            - heading "Ask this run of ${retiredJob} to stop?" [level=2]
            - paragraph: ${JSON.stringify(`This run, ${runningName}, belongs to a job this version of the ERP does not have. ${notHere}`)}
            - button "Keep it running"
            - button "Ask it to stop"
        `);
        await capture("job-run-stop-retired-question", stop);
        await stop.getByRole("button", { name: "Ask it to stop" }).click();
        await expect(shell.toast(`Asked the run of ${retiredJob} to stop.`)).toBeVisible();
        await expect(jobsPage.status(running)).toHaveText("Running Stop requested");
        await expect(cancelRunning).toBeFocused();

        await cancelRunning.click();
        await expect(cancellation).toMatchAriaSnapshot(`
          - alertdialog "Cancel this run of ${retiredJob}?":
            - heading "Cancel this run of ${retiredJob}?" [level=2]
            - paragraph: ${JSON.stringify(`This run, ${runningName}, was asked to stop, but it belongs to a job this version of the ERP does not have. ${notHere}`)}
            - button "Keep it running"
            - button "Cancel the run"
        `);
        await cancellation.getByRole("button", { name: "Cancel the run" }).click();
        const refusal = jobsPage.cancelError(running);
        await expect(refusal).toHaveRole("alert");
        await expect(refusal).toHaveText(stillRunning);
        await expect(cancelRunning).toBeFocused();
        await expect(jobsPage.status(running)).toHaveText("Running Stop requested");
        await capture("job-run-still-running", jobsPage.runsCard);
      });

      test("explains an action that a change elsewhere made out of date beside its button, again when it is retried", async ({
        page,
        api,
        request,
        capture,
      }) => {
        const jobsPage = new JobsPage(page);
        const accountCheck = await accountCheckOf(api);
        const prefix = runKeyPrefix("stale");
        const waitingKey = `${prefix}-waiting`;
        const gaveUpKey = `${prefix}-gave-up`;
        const waitingId = await seedJobRun(request, {
          jobName: retiredJob,
          idempotencyKey: waitingKey,
          status: "pending",
        });
        const gaveUpId = await seedJobRun(request, {
          jobName: accountCheckJob,
          idempotencyKey: gaveUpKey,
          status: "deadLettered",
        });

        await jobsPage.goto(`?key=${prefix}`);
        await jobsPage.waitUntilInteractive();
        const cancelledElsewhere = await api.post(`${jobRunApi(waitingId)}/cancel`);
        expect(cancelledElsewhere.status(), "the cancel made elsewhere").toBe(200);
        const rerunElsewhere = await api.post(`${jobRunApi(gaveUpId)}/rerun`);
        expect(rerunElsewhere.status(), "the run again made elsewhere").toBe(200);

        const waiting = await jobsPage.run(waitingKey);
        const cancel = jobsPage.cancel(waiting);
        const cancellation = jobsPage.confirmation(`Cancel this run of ${retiredJob}?`);
        await cancel.click();
        await cancellation.getByRole("button", { name: "Cancel the run" }).click();
        const refusal = jobsPage.cancelError(waiting);
        await expect(refusal).toHaveRole("alert");
        await expect(refusal).toContainText(finishedMeanwhile);
        await expect(refusal).toContainText("Reference: ");
        await expect(refusal).toBeInViewport();
        await expect(cancel).toBeFocused();
        await expect(jobsPage.status(waiting)).toHaveText("Waiting");
        await capture("job-run-cancel-refused", jobsPage.runsCard);

        const release = await holdServerFunctionCalls(page, jobsPath);
        await cancel.click();
        await cancellation.getByRole("button", { name: "Cancel the run" }).click();
        await expect(refusal).toHaveCount(0);
        await expect(cancel).toHaveAttribute("aria-busy", "true");
        release();
        await expect(refusal).toHaveRole("alert");
        await expect(refusal).toContainText(finishedMeanwhile);
        await expect(refusal).toContainText("Reference: ");
        await expect(cancel).not.toHaveAttribute("aria-busy");

        const gaveUp = await jobsPage.run(gaveUpKey);
        const rerun = jobsPage.rerun(gaveUp);
        const question = jobsPage.confirmation(`Run ${accountCheck.title} again?`);
        await rerun.click();
        await question.getByRole("button", { name: "Run again", exact: true }).click();
        const rerunRefusal = jobsPage.rerunError(gaveUp);
        await expect(rerunRefusal).toHaveRole("alert");
        await expect(rerunRefusal).toContainText(notRerunnable);
        await expect(rerunRefusal).toContainText("Reference: ");
        await expect(rerun).toBeFocused();
        await expect(jobsPage.status(gaveUp)).toHaveText("Gave up");

        const releaseRerun = await holdServerFunctionCalls(page, jobsPath);
        await rerun.click();
        await question.getByRole("button", { name: "Run again", exact: true }).click();
        await expect(rerunRefusal).toHaveCount(0);
        await expect(rerun).toHaveAttribute("aria-busy", "true");
        releaseRerun();
        await expect(rerunRefusal).toHaveRole("alert");
        await expect(rerunRefusal).toContainText(notRerunnable);
        await expect(rerun).not.toHaveAttribute("aria-busy");
      });

      test("keeps a refusal inside its run's card on a screen 360 or 320 pixels wide", async ({
        page,
        api,
        request,
        capture,
      }) => {
        const jobsPage = new JobsPage(page);
        const prefix = runKeyPrefix("narrow");
        const key = `${prefix}-waiting`;
        const id = await seedJobRun(request, { jobName: retiredJob, idempotencyKey: key, status: "pending" });
        await seedJobRun(request, {
          jobName: accountCheckJob,
          idempotencyKey: `${prefix}-failed`,
          status: "failed",
        });
        await page.setViewportSize({ width: 360, height: 800 });

        await jobsPage.goto(`?key=${prefix}`);
        await jobsPage.waitUntilInteractive();
        expect(await jobsPage.list.showsCards(), "the runs shown as cards").toBe(true);
        const cancelledElsewhere = await api.post(`${jobRunApi(id)}/cancel`);
        expect(cancelledElsewhere.status(), "the cancel made elsewhere").toBe(200);
        const run = await jobsPage.run(key);
        await jobsPage.cancel(run).click();
        await jobsPage
          .confirmation(`Cancel this run of ${retiredJob}?`)
          .getByRole("button", { name: "Cancel the run" })
          .click();
        await expect(jobsPage.cancelError(run)).toContainText(finishedMeanwhile);

        for (const width of [360, 320]) {
          await page.setViewportSize({ width, height: 800 });
          await expect
            .poll(() => jobsPage.widestCardOverflow(), { message: `the cards at ${width} pixels` })
            .toBeLessThanOrEqual(0);
          await capture(`job-run-cancel-refused-${width}`, jobsPage.runsCard);
        }
      });
    });
  }
});

test.describe("background jobs for an employee", () => {
  forEachTheme("tells them they have no access to the jobs or to a run", async ({ page, capture }) => {
    const shell = new AppShell(page);
    const id = await seedJobRun(page.request, {
      jobName: retiredJob,
      idempotencyKey: `${runKeyPrefix("employee")}-waiting`,
      status: "pending",
    });

    await page.goto(jobsPath);

    await expect(
      page.getByRole("heading", { name: "You do not have access to this page", level: 1 }),
    ).toBeVisible();
    await expect(page).toHaveTitle(/No access · ERP-AI-Pro/);
    await expect(page.getByTestId("jobs-card")).toHaveCount(0);
    await expect(page.getByTestId("job-runs-card")).toHaveCount(0);
    await expect(page.getByRole("main")).toMatchAriaSnapshot(`
      - main:
        - heading "You do not have access to this page" [level=1]
        - paragraph: Ask an administrator for access.
        - link "Go to the home page"
    `);
    await capture("jobs-no-access");
    await shell.openNavigation();
    await expect(shell.primaryNavigation).toBeVisible();
    await expect(shell.navigationLink("Background jobs")).toHaveCount(0);
    await shell.closeDrawer();

    await page.goto(jobRunPath(id));
    await expect(
      page.getByRole("heading", { name: "You do not have access to this page", level: 1 }),
    ).toBeVisible();
    await expect(page).toHaveTitle(/No access · ERP-AI-Pro/);
    await expect(page.getByTestId("job-run-details")).toHaveCount(0);
  });

  test("the API refuses them the jobs, the runs, a run and its actions", async ({ api, request }) => {
    const id = await seedJobRun(request, {
      jobName: retiredJob,
      idempotencyKey: `${runKeyPrefix("employee-api")}-failed`,
      status: "failed",
    });

    for (const path of [jobsApi, jobRunsApi, jobRunApi(id)]) {
      const refused = await api.get(path);
      expect(refused.status(), path).toBe(403);
      expect(await refused.json(), path).toMatchObject({ code: "request.forbidden" });
    }
    for (const path of [`${jobRunApi(id)}/rerun`, `${jobRunApi(id)}/cancel`]) {
      const refused = await api.post(path);
      expect(refused.status(), path).toBe(403);
      expect(await refused.json(), path).toMatchObject({ code: "request.forbidden" });
    }
  });
});
