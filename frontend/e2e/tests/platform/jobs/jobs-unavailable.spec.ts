import { randomUUID } from "node:crypto";

import { addUnverifiedSessionCookie } from "../../../fixtures/sign-in";
import { offlineBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { sessionRenewed } from "../../../pages/identity/auth/session.page";
import { JobRunPage } from "../../../pages/platform/jobs/run.page";
import { JobsPage } from "../../../pages/platform/jobs/runs.page";

test.describe("background jobs without the API", () => {
  test.skip(
    !offlineBaseURL,
    "E2E_OFFLINE_BASE_URL is not set and Playwright did not start the offline web instance",
  );
  test.use({
    baseURL: offlineBaseURL ?? "",
    persona: null,
    expectedConsoleError: /the server responded with a status of 500/,
  });
  test.beforeEach(async ({ context }) => {
    await addUnverifiedSessionCookie(context, offlineBaseURL ?? "");
  });

  forEachTheme(
    "explains that the jobs and their runs are unavailable instead of offering filters or actions",
    async ({ page, capture }) => {
      const jobsPage = new JobsPage(page);
      const renewal = sessionRenewed(page);
      await jobsPage.goto();
      expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);

      await expect(page).toHaveTitle(/Background jobs · ERP-AI-Pro/);
      await expect(jobsPage.jobsUnavailable).toHaveRole("status");
      await expect(jobsPage.jobsUnavailable).toContainText("The jobs are not available.");
      await expect(jobsPage.jobsUnavailable).toContainText("The API did not respond.");
      await expect(jobsPage.jobs).toHaveCount(0);
      await expect(jobsPage.runsUnavailable).toHaveRole("status");
      await expect(jobsPage.runsUnavailable).toContainText("The runs are not available.");
      await expect(jobsPage.runsUnavailable).toContainText("The API did not respond.");
      await expect(jobsPage.list.filters).toHaveCount(0);
      await expect(page.getByTestId("job-run-actions")).toHaveCount(0);
      await capture("jobs-unavailable");
    },
  );

  forEachTheme("explains that a run's details are unavailable", async ({ page, capture }) => {
    const runPage = new JobRunPage(page);
    await runPage.goto(randomUUID());

    await expect(page).toHaveTitle(/Job run · ERP-AI-Pro/);
    await expect(runPage.heading).toHaveText("Job run");
    await expect(runPage.unavailable).toHaveRole("status");
    await expect(runPage.unavailable).toContainText("This run's details are not available.");
    await expect(runPage.unavailable).toContainText("The API did not respond.");
    await expect(runPage.actions).toHaveCount(0);
    await capture("job-run-unavailable");
  });
});
