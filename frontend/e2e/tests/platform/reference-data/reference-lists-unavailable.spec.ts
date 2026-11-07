import { addUnverifiedSessionCookie } from "../../../fixtures/sign-in";
import { offlineBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { sessionRenewed } from "../../../pages/identity/auth/session.page";
import {
  countriesList,
  currenciesList,
  FinancialYearsPage,
  indianStatesList,
  ReferenceListPage,
} from "../../../pages/platform/reference-data/reference-lists.page";

test.describe("reference lists without the API", () => {
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
    "explain that the countries could not be read and offer no search",
    async ({ page, capture }) => {
      const list = new ReferenceListPage(page, countriesList);
      const renewal = sessionRenewed(page);
      await list.goto();
      expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);

      await expect(page).toHaveTitle("Countries · ERP-AI-Pro");
      await expect(list.tabs).toBeVisible();
      await expect(list.tab("Countries")).toHaveAttribute("aria-current", "page");
      await expect(list.card).toBeVisible();
      await expect(list.unavailable).toHaveRole("status");
      await expect(list.unavailable).toHaveText("The list of countries could not be read. Try again later.");
      await expect(list.search).toHaveCount(0);
      await expect(list.list).toHaveCount(0);
      await capture("countries-unavailable");
    },
  );

  test("explain it on the State codes, the currencies and the financial years too", async ({ page }) => {
    for (const { definition, message } of [
      {
        definition: indianStatesList,
        message: "The list of Indian states and union territories could not be read. Try again later.",
      },
      { definition: currenciesList, message: "The list of currencies could not be read. Try again later." },
    ]) {
      const list = new ReferenceListPage(page, definition);
      await list.goto();

      await expect(page).toHaveTitle(`${definition.title} · ERP-AI-Pro`);
      await expect(list.unavailable, definition.title).toHaveText(message);
      await expect(list.search, definition.title).toHaveCount(0);
      await expect(list.list, definition.title).toHaveCount(0);
    }

    const years = new FinancialYearsPage(page);
    await years.goto();
    await expect(page).toHaveTitle("Financial years · ERP-AI-Pro");
    await expect(years.unavailable).toHaveRole("status");
    await expect(years.unavailable).toHaveText(
      "The list of financial years could not be read. Try again later.",
    );
    await expect(years.chooser).toHaveCount(0);
    await expect(years.quarters).toHaveCount(0);
    await expect(years.periods).toHaveCount(0);
  });
});
