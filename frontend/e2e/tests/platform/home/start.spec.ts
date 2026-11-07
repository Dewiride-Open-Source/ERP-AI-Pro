import { employeeNavigation, registeredNavigation, startPageSnapshot } from "../../../fixtures/navigation";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { StartPage } from "../../../pages/platform/home/start.page";

test.describe("start page", () => {
  test.use({ persona: "administrator" });

  forEachTheme("greets the person and opens every area from its card", async ({ page, capture }) => {
    // Every card is opened and left again, so the budget grows with the registry instead of every new module shrinking the
    // time left for the others.
    test.setTimeout(30_000 + registeredNavigation.length * 2_500);
    const start = new StartPage(page);
    await start.goto();

    await expect(page).toHaveTitle(/Home · ERP-AI-Pro/);
    await expect(start.areas).toHaveCount(registeredNavigation.length);
    await expect(start.noAreas).toHaveCount(0);
    await expect(page.getByRole("main")).toMatchAriaSnapshot(
      startPageSnapshot(registeredNavigation, /^Welcome, /),
    );
    await capture("start");

    for (const entry of registeredNavigation) {
      await start.openLink(entry.title).click();
      await expect(page).toHaveURL((url) => url.pathname === entry.href);
      await page.goBack();
      await expect(start.heading).toBeVisible();
    }
  });

  test("names the signed-in person", async ({ page, person }) => {
    const start = new StartPage(page);
    await start.goto();

    await expect(start.heading).toHaveText(`Welcome, ${person?.name ?? ""}`);
  });
});

test.describe("start page for an employee", () => {
  forEachTheme("shows only the areas the person may open", async ({ page, capture }) => {
    const start = new StartPage(page);
    await start.goto();

    await expect(start.areas).toHaveCount(employeeNavigation.length);
    await expect(start.noAreas).toHaveCount(0);
    for (const lacking of registeredNavigation.filter((entry) => !entry.employees)) {
      await expect(start.openLink(lacking.title)).toHaveCount(0);
    }
    await expect(page.getByRole("main")).toMatchAriaSnapshot(
      startPageSnapshot(employeeNavigation, /^Welcome, /),
    );
    await capture("start-employee");

    for (const entry of employeeNavigation) {
      await start.openLink(entry.title).click();
      await expect(page).toHaveURL((url) => url.pathname === entry.href);
      await page.goBack();
      await expect(start.heading).toBeVisible();
    }
  });
});
