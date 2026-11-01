import type { APIRequestContext } from "@playwright/test";

import {
  accountCheckJob,
  retiredJob,
  runKeyPrefix,
  seedJobRun,
  type JobRunSeed,
  type JobRunStatus,
} from "../../../fixtures/job-runs";
import { holdServerFunctionCalls } from "../../../fixtures/server-functions";
import { expect, forEachTheme, test, themes, type SignedInApi } from "../../../fixtures/test";
import {
  jobRunApi,
  jobRunPath,
  jobRunsApi,
  jobsApi,
  JobsPage,
  jobsPath,
} from "../../../pages/platform/jobs/runs.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

type ApiJob = {
  readonly name: string;
  readonly title: string;
  readonly description: string;
  readonly maxAttempts: number;
  readonly schedule: { readonly expression: string; readonly nextOccurrenceAt: string | null } | null;
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
  { jobName: accountCheckJob, status: "failed" },
  { jobName: retiredJob, status: "running", stopRequested: true },
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

async function accountCheckOf(api: SignedInApi): Promise<ApiJob> {
  const response = await api.get(jobsApi);
  expect(response.status(), "the jobs").toBe(200);
  const job = ((await response.json()) as ApiJob[]).find((candidate) => candidate.name === accountCheckJob);
  expect(job, "the account check among the jobs").toBeDefined();
  return job!;
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
        await expect(jobsPage.jobsCard.getByTestId("job-problem")).toHaveCount(0);
        const job = jobsPage.job(accountCheck.title);
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
        if (await list.showsCards()) {
          await expect(failedRow).toMatchAriaSnapshot(`
            - listitem:
              - link "${accountCheck.title}":
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
              - ${quotedNode(`button "Run again: ${accountCheck.title}"`)}
              - ${quotedNode(`button "Cancel: ${accountCheck.title}"`)}
          `);
        } else {
          await expect(list.columnHeader("Created")).toHaveAttribute("aria-sort", "descending");
          await expect(failedRow).toMatchAriaSnapshot(`
            - row:
              - rowheader "${accountCheck.title}":
                - link "${accountCheck.title}":
                  - /url: ${jobRunPath(failed.id)}
              - cell ${JSON.stringify(failedStatus)}
              - cell "${failedKey}"
              - cell ${JSON.stringify(istDateTime(failed.createdAt))}
              - cell ${JSON.stringify(istDateTime(failed.dueAt))}
              - cell "Not finished"
              - cell "2 tries, 2 failed"
              - cell "The attempt failed."
              - cell:
                - ${quotedNode(`button "Run again: ${accountCheck.title}"`)}
                - ${quotedNode(`button "Cancel: ${accountCheck.title}"`)}
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

        await jobsPage.goto(`?key=${prefix}`);
        await jobsPage.waitUntilInteractive();
        const run = await jobsPage.run(key);
        await expect(jobsPage.status(run)).toHaveText("Gave up");
        await expect(jobsPage.cancel(run)).toHaveCount(0);
        const rerun = jobsPage.rerun(run);
        await expect(rerun).toHaveAccessibleName(`Run again: ${accountCheck.title}`);

        await rerun.click();
        const question = jobsPage.confirmation(`Run ${accountCheck.title} again?`);
        await expect(question).toMatchAriaSnapshot(`
          - alertdialog "Run ${accountCheck.title} again?":
            - heading "Run ${accountCheck.title} again?" [level=2]
            - paragraph: The ERP starts this run again in a moment, with all ${accountCheck.maxAttempts} of its tries available again. Its earlier tries stay in its history.
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
        await expect(jobsPage.runsHeading).toBeFocused();
        await expect(jobsPage.status(run)).toHaveText("Waiting");
        await expect(jobsPage.rerun(run)).toHaveCount(0);
        await expect(jobsPage.cancel(run)).toBeVisible();
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
        request,
        capture,
      }) => {
        const jobsPage = new JobsPage(page);
        const shell = new AppShell(page);
        const prefix = runKeyPrefix("cancel");
        const waitingKey = `${prefix}-waiting`;
        const runningKey = `${prefix}-running`;
        await seedJobRun(request, { jobName: retiredJob, idempotencyKey: waitingKey, status: "pending" });
        await seedJobRun(request, { jobName: retiredJob, idempotencyKey: runningKey, status: "running" });

        await jobsPage.goto(`?key=${prefix}`);
        await jobsPage.waitUntilInteractive();
        const waiting = await jobsPage.run(waitingKey);
        await expect(jobsPage.status(waiting)).toHaveText("Waiting");
        const cancelWaiting = jobsPage.cancel(waiting);
        await expect(cancelWaiting).toHaveAccessibleName(`Cancel: ${retiredJob}`);

        await cancelWaiting.click();
        const cancellation = jobsPage.confirmation(`Cancel this run of ${retiredJob}?`);
        await expect(cancellation).toMatchAriaSnapshot(`
          - alertdialog "Cancel this run of ${retiredJob}?":
            - heading "Cancel this run of ${retiredJob}?" [level=2]
            - paragraph: The run does not start. You can run it again later.
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
        await expect(shell.toast(`Cancelled the run of ${retiredJob}.`)).toBeVisible();
        await expect(jobsPage.runsHeading).toBeFocused();
        await expect(jobsPage.status(waiting)).toHaveText("Cancelled");
        await expect(jobsPage.cancel(waiting)).toHaveCount(0);
        await expect(jobsPage.rerun(waiting), "a run of a job the ERP no longer has").toHaveCount(0);

        const running = await jobsPage.run(runningKey);
        const cancelRunning = jobsPage.cancel(running);
        await expect(jobsPage.status(running)).toHaveText("Running");
        await cancelRunning.click();
        const stop = jobsPage.confirmation(`Ask this run of ${retiredJob} to stop?`);
        await expect(stop).toMatchAriaSnapshot(`
          - alertdialog "Ask this run of ${retiredJob} to stop?":
            - heading "Ask this run of ${retiredJob} to stop?" [level=2]
            - paragraph: The run stops within about a minute, once it notices the request. If it finishes before then, it is recorded as done.
            - button "Keep it running"
            - button "Ask it to stop"
        `);
        await capture("job-run-stop-question", stop);
        await stop.getByRole("button", { name: "Keep it running" }).click();
        await expect(stop).toHaveCount(0);
        await expect(cancelRunning).toBeFocused();

        await cancelRunning.click();
        await stop.getByRole("button", { name: "Ask it to stop" }).click();
        await expect(shell.toast(`Asked the run of ${retiredJob} to stop.`)).toBeVisible();
        await expect(jobsPage.runsHeading).toBeFocused();
        await expect(jobsPage.status(running)).toHaveText("Running Stop requested");
        await expect(jobsPage.cancel(running)).toHaveCount(0);
      });

      test("explains a cancel that a change elsewhere made out of date beside its button, again when it is retried", async ({
        page,
        api,
        request,
        capture,
      }) => {
        const jobsPage = new JobsPage(page);
        const prefix = runKeyPrefix("stale");
        const key = `${prefix}-waiting`;
        const id = await seedJobRun(request, { jobName: retiredJob, idempotencyKey: key, status: "pending" });

        await jobsPage.goto(`?key=${prefix}`);
        await jobsPage.waitUntilInteractive();
        const cancelledElsewhere = await api.post(`${jobRunApi(id)}/cancel`);
        expect(cancelledElsewhere.status(), "the cancel made elsewhere").toBe(200);

        const run = await jobsPage.run(key);
        const cancel = jobsPage.cancel(run);
        const cancellation = jobsPage.confirmation(`Cancel this run of ${retiredJob}?`);
        await cancel.click();
        await cancellation.getByRole("button", { name: "Cancel the run" }).click();
        const refusal = jobsPage.cancelError(run);
        await expect(refusal).toHaveRole("alert");
        await expect(refusal).toContainText(finishedMeanwhile);
        await expect(refusal).toContainText("Reference: ");
        await expect(refusal).toBeInViewport();
        await expect(cancel).toBeFocused();
        await expect(jobsPage.status(run)).toHaveText("Waiting");
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
