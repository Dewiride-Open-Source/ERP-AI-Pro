import { randomUUID } from "node:crypto";

import { accountCheckJob, retiredJob, runKeyPrefix, seedJobRun } from "../../../fixtures/job-runs";
import { expect, test, themes, type SignedInApi } from "../../../fixtures/test";
import { JobRunPage } from "../../../pages/platform/jobs/run.page";
import { jobRunApi, jobRunPath, jobsApi } from "../../../pages/platform/jobs/runs.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

type ApiJob = { readonly name: string; readonly title: string; readonly maxAttempts: number };

type ApiEvent = {
  readonly kind: string;
  readonly occurredAt: string;
  readonly attempt: number | null;
  readonly traceId: string | null;
  readonly retryAt: string | null;
};

type ApiHistory = {
  readonly run: { readonly idempotencyKey: string; readonly dueAt: string; readonly createdAt: string };
  readonly events: readonly ApiEvent[];
};

const graphTimedOut = "Microsoft Graph did not answer in time.";

const indiaDateTime = new Intl.DateTimeFormat("en-IN", {
  timeZone: "Asia/Kolkata",
  dateStyle: "medium",
  timeStyle: "short",
});

const indiaDay = new Intl.DateTimeFormat("en-IN", {
  timeZone: "Asia/Kolkata",
  weekday: "long",
  day: "numeric",
  month: "long",
  year: "numeric",
});

// An accessible name holding ": " reads as a YAML mapping inside a snapshot, so such a node is written as a quoted YAML string.
function quotedNode(node: string): string {
  return `'${node.replaceAll("'", "''")}'`;
}

function istDateTime(instant: string): string {
  return indiaDateTime.format(new Date(instant));
}

function istDay(instant: string): string {
  return indiaDay.format(new Date(instant));
}

async function accountCheckOf(api: SignedInApi): Promise<ApiJob> {
  const response = await api.get(jobsApi);
  expect(response.status(), "the jobs").toBe(200);
  const job = ((await response.json()) as ApiJob[]).find((candidate) => candidate.name === accountCheckJob);
  expect(job, "the account check among the jobs").toBeDefined();
  return job!;
}

async function historyOf(api: SignedInApi, id: string): Promise<ApiHistory> {
  const response = await api.get(jobRunApi(id));
  expect(response.status(), "the run's history").toBe(200);
  return (await response.json()) as ApiHistory;
}

function traceOf(history: ApiHistory, attempt: number): string {
  const started = history.events.find((event) => event.kind === "started" && event.attempt === attempt);
  expect(started?.traceId, `the reference of try ${attempt}`).toBeTruthy();
  return started?.traceId ?? "";
}

test.describe("job run", () => {
  test.use({ persona: "administrator" });

  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("shows a failed run's details and history, runs it again and cancels it once confirmed", async ({
        page,
        api,
        request,
        person,
        capture,
      }) => {
        const runPage = new JobRunPage(page);
        const shell = new AppShell(page);
        const accountCheck = await accountCheckOf(api);
        const key = `${runKeyPrefix("run")}-failed`;
        const id = await seedJobRun(request, {
          jobName: accountCheckJob,
          idempotencyKey: key,
          status: "failed",
          attempts: 2,
          errorMessage: graphTimedOut,
        });
        const history = await historyOf(api, id);
        const day = istDay(history.events[0]!.occurredAt);
        test.skip(
          history.events.some((event) => istDay(event.occurredAt) !== day),
          "The seeded history straddles midnight in India, so it falls on two days.",
        );
        const firstRetry = history.events.find((event) => event.kind === "failed" && event.attempt === 1);
        const name = person?.name ?? "";

        await runPage.goto(id);
        await runPage.waitUntilInteractive();

        await expect(page).toHaveTitle(`${accountCheck.title} · ERP-AI-Pro`);
        await expect(runPage.unavailable).toHaveCount(0);
        await expect(runPage.header).toMatchAriaSnapshot(`
          - paragraph: Background jobs
          - heading "${accountCheck.title}" [level=1]
          - paragraph: ${JSON.stringify(`Failed Tries again at ${istDateTime(history.run.dueAt)}`)}
          - ${quotedNode(`button "Run again: ${accountCheck.title}"`)}
          - ${quotedNode(`button "Cancel: ${accountCheck.title}"`)}
        `);
        await expect(runPage.details).toMatchAriaSnapshot(`
          - heading "Details" [level=2]
          - term: Job
          - definition: ${accountCheck.title}
          - term: Key
          - definition: ${key}
          - term: Requested by
          - definition: The ERP
          - term: Created
          - definition: ${JSON.stringify(istDateTime(history.run.createdAt))}
          - term: Due
          - definition: ${JSON.stringify(istDateTime(history.run.dueAt))}
          - term: Latest try started
          - term: Finished
          - definition: Not finished
          - term: Tries
          - definition: 2 tries, 2 failed
          - term: Gives up
          - definition: After ${accountCheck.maxAttempts} failed tries
          - term: Last error
          - definition: ${graphTimedOut}
        `);
        await expect(runPage.timeline).toMatchAriaSnapshot(`
          - list "History":
            - listitem:
              - heading "${day}" [level=3]
              - list "${day}":
                - listitem:
                  - time: /^\\d{1,2}:\\d{2} [ap]m IST$/
                  - paragraph: The ERP asked for this run.
                - listitem:
                  - time: /^\\d{1,2}:\\d{2} [ap]m IST$/
                  - paragraph: Try 1 started.
                  - paragraph: "Reference: ${traceOf(history, 1)}"
                - listitem:
                  - time: /^\\d{1,2}:\\d{2} [ap]m IST$/
                  - paragraph: Try 1 failed.
                  - paragraph: ${graphTimedOut}
                  - paragraph: ${JSON.stringify(`It tries again at ${istDateTime(firstRetry?.retryAt ?? "")}.`)}
                - listitem:
                  - time: /^\\d{1,2}:\\d{2} [ap]m IST$/
                  - paragraph: Try 2 started.
                  - paragraph: "Reference: ${traceOf(history, 2)}"
                - listitem:
                  - time: /^\\d{1,2}:\\d{2} [ap]m IST$/
                  - paragraph: Try 2 failed.
                  - paragraph: ${graphTimedOut}
                  - paragraph: ${JSON.stringify(`It tries again at ${istDateTime(history.run.dueAt)}.`)}
        `);
        await capture("job-run");

        await runPage.rerun.click();
        const question = runPage.confirmation(`Run ${accountCheck.title} again?`);
        await expect(question).toBeVisible();
        await question.getByRole("button", { name: "Keep it as it is" }).click();
        await expect(question).toHaveCount(0);
        await expect(runPage.rerun).toBeFocused();
        await runPage.rerun.click();
        await question.getByRole("button", { name: "Run again", exact: true }).click();
        await expect(shell.toast(`${accountCheck.title} will run again in a moment.`)).toBeVisible();
        await expect(runPage.statusLine).toBeFocused();
        await expect(runPage.statusLine).toHaveText("Waiting");
        await expect(runPage.rerun).toHaveCount(0);
        await expect
          .poll(async () => (await runPage.summaries()).at(-1))
          .toBe(`${name} set this run to run again.`);
        await expect(runPage.detail("Last error")).toHaveText("None");

        await runPage.cancel.click();
        const cancellation = runPage.confirmation(`Cancel this run of ${accountCheck.title}?`);
        await expect(cancellation).toContainText("The run does not start. You can run it again later.");
        await cancellation.getByRole("button", { name: "Keep the run" }).click();
        await expect(cancellation).toHaveCount(0);
        await expect(runPage.cancel).toBeFocused();
        await runPage.cancel.click();
        await cancellation.getByRole("button", { name: "Cancel the run" }).click();
        await expect(shell.toast(`Cancelled the run of ${accountCheck.title}.`)).toBeVisible();
        await expect(runPage.statusLine).toBeFocused();
        await expect(runPage.statusLine).toHaveText("Cancelled");
        await expect(runPage.cancel).toHaveCount(0);
        await expect(runPage.rerun).toBeVisible();
        await expect.poll(async () => (await runPage.summaries()).at(-1)).toBe(`${name} cancelled this run.`);
        await expect(runPage.detail("Finished")).not.toHaveText("Not finished");
        await capture("job-run-cancelled", runPage.header);
      });

      test("shows a run of a job this version of the ERP no longer has, with nothing to do", async ({
        page,
        request,
        capture,
      }) => {
        const runPage = new JobRunPage(page);
        const id = await seedJobRun(request, {
          jobName: retiredJob,
          idempotencyKey: `${runKeyPrefix("retired")}-gave-up`,
          status: "deadLettered",
          attempts: 3,
        });

        await runPage.goto(id);
        await runPage.waitUntilInteractive();

        await expect(page).toHaveTitle(`${retiredJob} · ERP-AI-Pro`);
        await expect(runPage.heading).toHaveText(retiredJob);
        await expect(runPage.statusLine).toHaveText("Gave up");
        await expect(runPage.actions).toHaveCount(0);
        await expect(runPage.detail("Job")).toHaveText(`${retiredJob} (not in this version of the ERP)`);
        await expect(runPage.detail("Gives up")).toHaveText(
          "Not known: this version of the ERP does not have this job",
        );
        await expect(runPage.detail("Due")).toHaveCount(0);
        expect((await runPage.summaries()).at(-1)).toBe("Try 3 failed, so the ERP gave up on this run.");
        await capture("job-run-retired-job");
      });

      test("shows a running run that someone asked to stop, without a second request", async ({
        page,
        request,
        person,
      }) => {
        const runPage = new JobRunPage(page);
        const name = person?.name ?? "";
        const id = await seedJobRun(request, {
          jobName: retiredJob,
          idempotencyKey: `${runKeyPrefix("stopping")}-running`,
          status: "running",
          stopRequested: true,
          requestedBy: person?.id ?? "",
        });

        await runPage.goto(id);
        await runPage.waitUntilInteractive();

        await expect(runPage.statusLine).toHaveText("Running Stop requested");
        await expect(runPage.actions).toHaveCount(0);
        await expect(runPage.detail("Requested by")).toHaveText(name);
        expect(await runPage.summaries()).toEqual([
          `${name} asked for this run.`,
          "Try 1 started.",
          `${name} asked this run to stop.`,
        ]);
      });
    });
  }

  test.describe("that does not exist", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 404/ });

    test("answers not found inside the shell", async ({ page, capture }) => {
      const shell = new AppShell(page);

      for (const id of [randomUUID(), "not-a-run"]) {
        const response = await page.goto(jobRunPath(id));

        expect(response?.status(), id).toBe(404);
        await expect(shell.banner).toBeVisible();
        await expect(page.getByRole("heading", { name: "This page does not exist", level: 1 })).toBeVisible();
      }
      await capture("job-run-not-found");
    });
  });
});
