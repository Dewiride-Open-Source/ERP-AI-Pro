import { randomUUID } from "node:crypto";

import { gatedBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { jobRunApi, jobRunPath, jobRunsApi, jobsApi, jobsPath } from "../../../pages/platform/jobs/runs.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

// An administrator holds every jobs permission, so only the disabled module can hide its pages from them.
test.describe("background jobs with the jobs module disabled", () => {
  test.skip(
    !gatedBaseURL,
    "E2E_GATED_BASE_URL is not set and Playwright did not start the gated web instance",
  );
  test.use({ baseURL: gatedBaseURL ?? "", persona: "administrator" });

  test.describe("its pages", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 404/ });

    forEachTheme("hide the module and answer not found inside the shell", async ({ page, capture }) => {
      const shell = new AppShell(page);

      const response = await page.goto(jobsPath);

      expect(response?.status()).toBe(404);
      await expect(shell.banner).toBeVisible();
      await expect(page.getByRole("heading", { name: "This page does not exist" })).toBeVisible();
      await shell.openNavigation();
      await expect(shell.primaryNavigation).toBeVisible();
      await expect(shell.navigationLink("Background jobs")).toHaveCount(0);
      await expect(shell.primaryNavigation.getByRole("link")).toHaveText(["Home"]);
      await shell.closeDrawer();
      await capture("jobs-disabled");

      const run = await page.goto(jobRunPath(randomUUID()));
      expect(run?.status()).toBe(404);
      await expect(page.getByRole("heading", { name: "This page does not exist" })).toBeVisible();
    });
  });

  test("the API answers the jobs, the runs, a run and its actions with the disabled module's problem", async ({
    api,
  }) => {
    const run = jobRunApi(randomUUID());
    const reads = [jobsApi, jobRunsApi, run].map(async (path) => ({ path, response: await api.get(path) }));
    const changes = [`${run}/rerun`, `${run}/cancel`].map(async (path) => ({
      path,
      response: await api.post(path),
    }));

    for (const { path, response } of await Promise.all([...reads, ...changes])) {
      expect(response.status(), path).toBe(404);
      expect(response.headers()["content-type"], path).toContain("application/problem+json");
      expect(await response.json(), path).toMatchObject({ code: "feature.disabled", instance: path });
    }
  });
});
