import { randomUUID } from "node:crypto";

import { gatedBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { peopleApi, peoplePath } from "../../../pages/identity/users/people.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

// An administrator holds every permission of the users module, so only the disabled module can hide its pages from them.
test.describe("people pages with the users module disabled", () => {
  test.skip(
    !gatedBaseURL,
    "E2E_GATED_BASE_URL is not set and Playwright did not start the gated web instance",
  );
  test.use({ baseURL: gatedBaseURL ?? "", persona: "administrator" });

  test.describe("its pages", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 404/ });

    forEachTheme(
      "hides the module and answers not found for the people list inside the shell",
      async ({ page, capture }) => {
        const shell = new AppShell(page);

        const response = await page.goto(peoplePath);

        expect(response?.status()).toBe(404);
        await expect(shell.banner).toBeVisible();
        await expect(page.getByRole("heading", { name: "This page does not exist" })).toBeVisible();
        await shell.openNavigation();
        await expect(shell.primaryNavigation).toBeVisible();
        await expect(shell.navigationLink("People")).toHaveCount(0);
        await expect(shell.primaryNavigation.getByRole("link")).toHaveText(["Home"]);
        await shell.closeDrawer();
        await capture("people-disabled");
      },
    );

    test("answers not found for every other page of the module", async ({ page }) => {
      const person = randomUUID();
      for (const path of [
        `${peoplePath}/new`,
        `${peoplePath}/permissions`,
        `${peoplePath}/${person}`,
        `${peoplePath}/${person}/edit`,
      ]) {
        const response = await page.goto(path);

        expect(response?.status(), path).toBe(404);
        await expect(page.getByRole("heading", { name: "This page does not exist" }), path).toBeVisible();
      }
    });
  });

  test("the API answers the people routes with the disabled module's problem", async ({ api }) => {
    for (const path of [peopleApi, `${peopleApi}/permissions`]) {
      const gated = await api.get(path);
      expect(gated.status(), path).toBe(404);
      expect(gated.headers()["content-type"], path).toContain("application/problem+json");
      expect(await gated.json(), path).toMatchObject({ code: "feature.disabled", instance: path });
    }
  });
});
