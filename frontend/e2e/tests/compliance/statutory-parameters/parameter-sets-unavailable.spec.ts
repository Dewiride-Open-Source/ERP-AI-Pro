import { addUnverifiedSessionCookie } from "../../../fixtures/sign-in";
import { offlineBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { sessionRenewed } from "../../../pages/identity/auth/session.page";
import {
  centralTaxRatesKey,
  ParameterSetPage,
} from "../../../pages/compliance/statutory-parameters/parameter-set.page";
import {
  parameterSetPath,
  ParameterSetsPage,
} from "../../../pages/compliance/statutory-parameters/parameter-sets.page";

test.describe("statutory parameters without the API", () => {
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

  forEachTheme("explain that the sets could not be read and list none", async ({ page, capture }) => {
    const listPage = new ParameterSetsPage(page);
    const renewal = sessionRenewed(page);
    await listPage.goto();
    expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);

    await expect(page).toHaveTitle("Statutory parameters · ERP-AI-Pro");
    await expect(listPage.overview).toBeVisible();
    await expect(listPage.unavailable).toHaveRole("status");
    await expect(listPage.unavailable).toHaveText(
      "The statutory parameter sets could not be read. Try again later.",
    );
    await expect(listPage.region).toHaveCount(0);
    await expect(page.getByTestId("searched-list")).toHaveCount(0);
    await capture("parameter-sets-unavailable");
  });

  forEachTheme("explain that a set could not be read and show none of it", async ({ page, capture }) => {
    const setPage = new ParameterSetPage(page);
    const renewal = sessionRenewed(page);
    await setPage.goto(parameterSetPath(centralTaxRatesKey), "Statutory parameter set");
    expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);

    await expect(page).toHaveTitle("Statutory parameter set · ERP-AI-Pro");
    await expect(setPage.unavailable).toHaveRole("status");
    await expect(setPage.unavailable).toHaveText(
      "This statutory parameter set could not be read. Try again later.",
    );
    await expect(setPage.versionsCard).toHaveCount(0);
    await expect(setPage.entriesCard).toHaveCount(0);
    await expect(setPage.changesCard).toHaveCount(0);
    await capture("parameter-set-unavailable");
  });
});
