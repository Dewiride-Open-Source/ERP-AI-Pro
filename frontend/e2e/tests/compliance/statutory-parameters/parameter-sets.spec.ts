import { assignRoles } from "../../../fixtures/records/person-records";
import { expect, forEachTheme, tabOntoLink, test, themes } from "../../../fixtures/test";
import {
  centralTaxRatesKey,
  ParameterSetPage,
} from "../../../pages/compliance/statutory-parameters/parameter-set.page";
import {
  parameterSetApi,
  parameterSetPath,
  parameterSetsApi,
  parameterSetsPath,
  ParameterSetsPage,
  parameterSetsSnapshot,
  readParameterSets,
} from "../../../pages/compliance/statutory-parameters/parameter-sets.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

const noAccess = "You do not have access to this page";

// The accountant holds the Employee permissions alone, so an administrator gives them the Finance role first, which counts
// from their next request; the sets are read through the page's own session, as the page reads them.
test.describe("statutory parameters for a finance person", () => {
  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("finds the entry under Compliance, sees every set with the version in force today and opens one by keyboard", async ({
        page,
        person,
        request,
        capture,
        isMobile,
      }) => {
        const shell = new AppShell(page);
        const listPage = new ParameterSetsPage(page);
        const setPage = new ParameterSetPage(page);
        await assignRoles(request, person, ["finance"]);
        const sets = await readParameterSets(page.request);
        const chosen = sets.find((set) => set.key === centralTaxRatesKey) ?? sets[0];
        expect(chosen, "a set to open").toBeDefined();

        await page.goto("/");
        await shell.waitUntilInteractive();
        await shell.openNavigation();
        const compliance = shell.primaryNavigation.getByRole("list", { name: "Compliance", exact: true });
        await expect(compliance.getByRole("link")).toHaveText(["Statutory parameters"]);
        await compliance.getByRole("link", { name: "Statutory parameters", exact: true }).click();
        await expect(page).toHaveURL((url) => url.pathname === parameterSetsPath && url.search === "");
        await expect(listPage.heading).toBeVisible();
        await expect(page).toHaveTitle("Statutory parameters · ERP-AI-Pro");
        await expect(listPage.overview).toBeVisible();
        await expect(listPage.unavailable).toHaveCount(0);
        await expect(listPage.cards).toHaveCount(sets.length);
        await expect(listPage.list).toMatchAriaSnapshot(parameterSetsSnapshot(sets));
        // A phone stacks every set's description in one column, which would make the whole page taller than a capture may be.
        await capture("parameter-sets", isMobile ? listPage.card(chosen!.title) : undefined);

        await tabOntoLink(page, listPage.cardLink(chosen!.title));
        await page.keyboard.press("Enter");
        await expect(page).toHaveURL(
          (url) => url.pathname === parameterSetPath(chosen!.key) && url.search === "",
        );
        await expect(setPage.heading).toHaveText(chosen!.title);
        await expect(page).toHaveTitle(`${chosen!.title} · ERP-AI-Pro`);
        await expect(setPage.versions).toBeVisible();
      });
    });
  }
});

test.describe("statutory parameters for an administrator", () => {
  test.use({ persona: "administrator" });

  forEachTheme("show the entry under Compliance and every set", async ({ page, capture }) => {
    const shell = new AppShell(page);
    const listPage = new ParameterSetsPage(page);
    const sets = await readParameterSets(page.request);

    await page.goto("/");
    await shell.waitUntilInteractive();
    await shell.openNavigation();
    const compliance = shell.primaryNavigation.getByRole("list", { name: "Compliance", exact: true });
    await expect(compliance.getByRole("link", { name: "Statutory parameters", exact: true })).toBeVisible();
    await shell.closeDrawer();

    await listPage.goto();
    await expect(page).toHaveTitle("Statutory parameters · ERP-AI-Pro");
    await expect(listPage.list).toMatchAriaSnapshot(parameterSetsSnapshot(sets));
    await capture("parameter-sets-administrator", listPage.overview.locator("header"));
  });
});

test.describe("statutory parameters for an employee", () => {
  forEachTheme(
    "hide the entry and tell them they have no access to either page",
    async ({ page, capture }) => {
      const shell = new AppShell(page);
      const listPage = new ParameterSetsPage(page);

      await page.goto("/");
      await shell.waitUntilInteractive();
      await shell.openNavigation();
      await expect(shell.primaryNavigation).toBeVisible();
      await expect(shell.navigationLink("Statutory parameters")).toHaveCount(0);
      await expect(
        shell.primaryNavigation.getByRole("list", { name: "Compliance", exact: true }),
      ).toHaveCount(0);
      await shell.closeDrawer();

      await page.goto(parameterSetsPath);
      await expect(page.getByRole("heading", { name: noAccess, level: 1 })).toBeVisible();
      await expect(page).toHaveTitle("No access · ERP-AI-Pro");
      await expect(listPage.overview).toHaveCount(0);
      await capture("parameter-sets-no-access");

      await page.goto(parameterSetPath(centralTaxRatesKey));
      await expect(page.getByRole("heading", { name: noAccess, level: 1 })).toBeVisible();
      await expect(page).toHaveTitle("No access · ERP-AI-Pro");
      await expect(new ParameterSetPage(page).overview).toHaveCount(0);
    },
  );

  test("the API refuses them the sets and a set", async ({ api }) => {
    for (const path of [parameterSetsApi, parameterSetApi(centralTaxRatesKey)]) {
      const response = await api.get(path);

      expect(response.status(), path).toBe(403);
      expect(await response.json(), path).toMatchObject({ code: "request.forbidden" });
    }
  });
});
