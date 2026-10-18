import { randomUUID } from "node:crypto";

import { addUnverifiedSessionCookie } from "../../../fixtures/sign-in";
import { offlineBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { sessionRenewed } from "../../../pages/identity/auth/session.page";
import { EditPersonPage, editPersonPath } from "../../../pages/identity/users/edit-person.page";
import { PersonPage } from "../../../pages/identity/users/person.page";

test.describe("person pages without the API", () => {
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
    "explains that the person's details are not available and offers nothing to change",
    async ({ page, capture }) => {
      const personPage = new PersonPage(page);
      const main = page.getByRole("main");
      const renewal = sessionRenewed(page);
      await personPage.goto(randomUUID());
      expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);

      await expect(page).toHaveTitle("Person · ERP-AI-Pro");
      await expect(personPage.heading).toHaveText("Person");
      await expect(personPage.unavailable).toHaveRole("status");
      await expect(personPage.unavailable).toContainText("This person's details are not available.");
      await expect(personPage.unavailable).toContainText("The API did not respond.");
      await expect(personPage.actions).toHaveCount(0);
      await expect(personPage.details).toHaveCount(0);
      await expect(main.getByRole("button")).toHaveCount(0);
      await capture("person-unavailable");

      const editPage = new EditPersonPage(page);
      const editRenewal = sessionRenewed(page);
      await page.goto(editPersonPath(randomUUID()));
      await expect(editPage.heading).toBeVisible();
      expect((await editRenewal).status(), "the renewal the edit page sends without its API").toBe(500);

      await expect(page).toHaveTitle("Edit a person · ERP-AI-Pro");
      await expect(editPage.heading).toHaveText("Edit a person");
      await expect(editPage.unavailable).toHaveRole("status");
      await expect(editPage.unavailable).toContainText("This person's details are not available.");
      await expect(editPage.unavailable).toContainText("The API did not respond.");
      await expect(page.getByTestId("edit-person-form")).toHaveCount(0);
      await expect(main.getByRole("button")).toHaveCount(0);
      await capture("person-edit-unavailable");
    },
  );
});
