import { randomUUID } from "node:crypto";

import {
  numberedDocumentsApi,
  numberSeriesApi,
  numberSeriesApiOf,
  untouchedKind,
} from "../../../fixtures/records/numbering-records";
import { gatedBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { numberingPath } from "../../../pages/platform/numbering/numbering.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

// An administrator holds every numbering permission, so only the disabled module can hide its page from them.
test.describe("document numbering with the numbering module disabled", () => {
  test.skip(
    !gatedBaseURL,
    "E2E_GATED_BASE_URL is not set and Playwright did not start the gated web instance",
  );
  test.use({ baseURL: gatedBaseURL ?? "", persona: "administrator" });

  test.describe("its page", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 404/ });

    forEachTheme("hides the module and answers not found inside the shell", async ({ page, capture }) => {
      const shell = new AppShell(page);

      const response = await page.goto(numberingPath);

      expect(response?.status()).toBe(404);
      await expect(shell.banner).toBeVisible();
      await expect(page.getByRole("heading", { name: "This page does not exist" })).toBeVisible();
      await shell.openNavigation();
      await expect(shell.primaryNavigation).toBeVisible();
      await expect(shell.navigationLink("Document numbering")).toHaveCount(0);
      await expect(shell.primaryNavigation.getByRole("link")).toHaveText(["Home"]);
      await shell.closeDrawer();
      await capture("numbering-disabled");
    });
  });

  test("the API answers the kinds of document, a numbering, a set-up and a change with the disabled module's problem", async ({
    api,
  }) => {
    const series = numberSeriesApiOf(randomUUID());
    const format = { prefix: "", year: "none", separator: "", digits: 1 };
    const answers = [
      { path: numberedDocumentsApi, response: await api.get(numberedDocumentsApi) },
      { path: series, response: await api.get(series) },
      {
        path: numberSeriesApi,
        response: await api.post(numberSeriesApi, {
          headers: { "Idempotency-Key": randomUUID() },
          data: { documentType: untouchedKind.documentType, format, nextNumber: 1 },
        }),
      },
      {
        path: series,
        response: await api.put(series, { data: { format, nextNumber: null, version: "AAAAAAAAB9E=" } }),
      },
    ];

    for (const { path, response } of answers) {
      expect(response.status(), path).toBe(404);
      expect(response.headers()["content-type"], path).toContain("application/problem+json");
      expect(await response.json(), path).toMatchObject({ code: "feature.disabled", instance: path });
    }
  });
});
