import { randomUUID } from "node:crypto";

import {
  approvalCountsApi,
  approvalKind,
  approvalKindSettingApi,
  approvalKindSettingsApi,
  approvalKindsApi,
  approvalRequestApi,
  approvalRequestsApi,
  askForApproval,
} from "../../../fixtures/records/approval-records";
import { gatedBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { approvalPagePaths, approvalRequestPath } from "../../../pages/platform/approvals/approvals.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

const version = "AAAAAAAAB9E=";

// An administrator holds every approvals permission, so only the disabled module can hide its pages from them.
test.describe("approvals with the approvals module disabled", () => {
  test.skip(
    !gatedBaseURL,
    "E2E_GATED_BASE_URL is not set and Playwright did not start the gated web instance",
  );
  test.use({ baseURL: gatedBaseURL ?? "", persona: "administrator" });

  test.describe("its pages", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 404/ });

    forEachTheme("hide the module and answer not found inside the shell", async ({ page, capture }) => {
      const shell = new AppShell(page);

      const response = await page.goto(approvalPagePaths.waiting);

      expect(response?.status()).toBe(404);
      await expect(shell.banner).toBeVisible();
      await expect(page.getByRole("heading", { name: "This page does not exist" })).toBeVisible();
      await shell.openNavigation();
      await expect(shell.primaryNavigation).toBeVisible();
      await expect(shell.navigationLink("Approvals")).toHaveCount(0);
      await expect(shell.primaryNavigation.getByRole("link")).toHaveText(["Home"]);
      await shell.closeDrawer();
      await capture("approvals-disabled");

      for (const path of [
        approvalPagePaths.mine,
        approvalPagePaths.decided,
        approvalPagePaths.kinds,
        approvalPagePaths.all,
        approvalRequestPath(randomUUID()),
      ]) {
        const other = await page.goto(path);
        expect(other?.status(), path).toBe(404);
        await expect(page.getByRole("heading", { name: "This page does not exist" })).toBeVisible();
      }
    });
  });

  test("the API answers every approvals route with the disabled module's problem", async ({ api }) => {
    const request = approvalRequestApi(randomUUID());
    const reads = [
      approvalRequestsApi,
      `${approvalRequestsApi}?box=all`,
      approvalCountsApi,
      request,
      approvalKindsApi,
    ].map(async (path) => ({ path: path.split("?")[0] ?? path, response: await api.get(path) }));
    const decisions = ["approve", "return", "reject", "withdraw"].map(async (action) => {
      const path = `${request}/${action}`;
      return { path, response: await api.post(path, { data: { version, comment: null } }) };
    });
    const setting = approvalKindSettingApi(randomUUID());
    const settings = [
      (async () => ({
        path: approvalKindSettingsApi,
        response: await api.post(approvalKindSettingsApi, {
          headers: { "Idempotency-Key": randomUUID() },
          data: { kind: "test.approvals.untouched-01", required: false, workingDaysToDecide: 3 },
        }),
      }))(),
      (async () => ({
        path: setting,
        response: await api.put(setting, { data: { required: false, workingDaysToDecide: 3, version } }),
      }))(),
    ];

    for (const { path, response } of await Promise.all([...reads, ...decisions, ...settings])) {
      expect(response.status(), path).toBe(404);
      expect(response.headers()["content-type"], path).toContain("application/problem+json");
      expect(await response.json(), path).toMatchObject({ code: "feature.disabled", instance: path });
    }
  });

  // The module serves the routes and the page, while the building block lets every module ask for approval in its own commands,
  // so a record still waits for approval with the module off, and nobody can see or decide it until it is on again.
  test("a record still asks for approval while the module is off, though nobody can see or decide it", async ({
    api,
    context,
  }, testInfo) => {
    const id = await askForApproval(context.request, approvalKind(testInfo, "light", "finance"));

    const response = await api.get(approvalRequestApi(id));

    expect(response.status(), "the request's page while the module is off").toBe(404);
    expect(await response.json()).toMatchObject({ code: "feature.disabled" });
  });
});
