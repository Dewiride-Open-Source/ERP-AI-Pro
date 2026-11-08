import { addUnverifiedSessionCookie } from "../../../fixtures/sign-in";
import { offlineBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { sessionRenewed } from "../../../pages/identity/auth/session.page";
import { NumberingPage } from "../../../pages/platform/numbering/numbering.page";

test.describe("document numbering without the API", () => {
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

  forEachTheme("explains that the kinds of document are not available", async ({ page, capture }) => {
    const numbering = new NumberingPage(page);
    const renewal = sessionRenewed(page);
    await numbering.goto();
    expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);

    await expect(page).toHaveTitle("Document numbering · ERP-AI-Pro");
    await expect(numbering.card).toBeVisible();
    await expect(numbering.unavailable).toHaveRole("status");
    await expect(numbering.unavailable).toContainText("The kinds of document are not available.");
    await expect(numbering.unavailable).toContainText("The API did not respond.");
    await expect(numbering.list).toHaveCount(0);
    await expect(page.getByTestId("numbering-action")).toHaveCount(0);
    await capture("numbering-unavailable");
  });
});
