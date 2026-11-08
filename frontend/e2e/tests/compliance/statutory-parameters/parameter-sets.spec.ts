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
  readParameterSets,
  setKeys,
  setRow,
  setRowsSnapshot,
  setsPerPage,
  setsShownCount,
} from "../../../pages/compliance/statutory-parameters/parameter-sets.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";
import { expectedMatches, searchedListAddress } from "../../../pages/shared/lists/searched-list.page";

const noAccess = "You do not have access to this page";

// The accountant holds the Employee permissions alone, so an administrator gives them the Finance role first, which counts
// from their next request; the sets are read through the page's own session, as the page reads them. The API answers the
// sets by title, and the list shows them in that order, ten a page.
test.describe("statutory parameters for a finance person", () => {
  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("finds the entry under Compliance, sees the sets a page at a time and opens one by keyboard", async ({
        page,
        person,
        request,
        capture,
      }) => {
        const shell = new AppShell(page);
        const listPage = new ParameterSetsPage(page);
        const setPage = new ParameterSetPage(page);
        await assignRoles(request, person, ["finance"]);
        const sets = await readParameterSets(page.request);
        const pageCount = Math.ceil(sets.length / setsPerPage);
        const chosen = sets[0]!;

        await page.goto("/");
        await shell.waitUntilInteractive();
        await shell.openNavigation();
        const compliance = shell.primaryNavigation.getByRole("list", { name: "Compliance", exact: true });
        await expect(compliance.getByRole("link")).toHaveText(["Statutory parameters"]);
        await compliance.getByRole("link", { name: "Statutory parameters", exact: true }).click();
        await expect(page).toHaveURL((url) => url.pathname === parameterSetsPath && url.search === "");
        await expect(listPage.heading).toBeVisible();
        await expect(page).toHaveTitle("Statutory parameters · ERP-AI-Pro");
        await listPage.waitUntilInteractive();
        await expect(listPage.unavailable).toHaveCount(0);
        await expect(listPage.entries.search).toMatchAriaSnapshot(`
          - search "Search parameter sets":
            - group:
              - text: Title or words
              - group:
                - group
                - searchbox "Title or words"
            - button "Search"
        `);
        await expect(listPage.entries.status).toHaveText(setsShownCount(sets.length));
        expect(await listPage.entries.rows()).toEqual(sets.slice(0, setsPerPage).map(setRow));
        const cards = await listPage.entries.showsCards();
        await expect(listPage.shownList(cards)).toMatchAriaSnapshot(
          setRowsSnapshot(sets.slice(0, setsPerPage), cards),
        );
        await capture("parameter-sets");

        if (pageCount > 1) {
          await listPage.entries.pageLink("Next page").click();
          await expect(page).toHaveURL((url) => url.searchParams.get("page") === "2");
          await expect(listPage.entries.pageLabel).toHaveText(`Page 2 of ${pageCount}`);
          await expect
            .poll(() => listPage.entries.rows())
            .toEqual(sets.slice(setsPerPage, 2 * setsPerPage).map(setRow));
          await listPage.entries.pageLink("First page").click();
          await expect(page).toHaveURL((url) => url.pathname === parameterSetsPath && url.search === "");
        }

        await tabOntoLink(page, listPage.setLink(chosen.title));
        await page.keyboard.press("Enter");
        await expect(page).toHaveURL(
          (url) => url.pathname === parameterSetPath(chosen.key) && url.search === "",
        );
        await expect(setPage.heading).toHaveText(chosen.title);
        await expect(page).toHaveTitle(`${chosen.title} · ERP-AI-Pro`);
        await expect(setPage.versions).toBeVisible();
      });

      test("finds the sets by the words of their titles and descriptions", async ({
        page,
        person,
        request,
        capture,
      }) => {
        const listPage = new ParameterSetsPage(page);
        await assignRoles(request, person, ["finance"]);
        const sets = await readParameterSets(page.request);
        const words =
          sets.find((set) => set.key === centralTaxRatesKey)?.title.split(" ")[0] ?? sets[0]!.title;
        const found = expectedMatches(sets, words, setKeys);
        expect(found.length, `sets whose words hold ${words}`).toBeGreaterThan(0);
        await listPage.goto();
        await listPage.waitUntilInteractive();

        await listPage.entries.searchFor(words);
        await expect(page).toHaveURL((url) => url.searchParams.get("q") === words);
        await expect(listPage.entries.status).toHaveText(setsShownCount(found.length));
        await expect.poll(() => listPage.entries.rows()).toEqual(found.slice(0, setsPerPage).map(setRow));
        await expect(listPage.entries.searchBox).toBeFocused();
        await capture("parameter-sets-found", listPage.region);

        const byKey = expectedMatches(sets, centralTaxRatesKey, setKeys);
        await listPage.entries.searchBox.fill(centralTaxRatesKey);
        await listPage.entries.searchButton.click();
        await expect
          .poll(() => listPage.entries.titles())
          .toEqual(byKey.slice(0, setsPerPage).map((set) => set.title));

        await listPage.entries.clearSearch.click();
        await expect(page).toHaveURL((url) => url.pathname === parameterSetsPath && url.search === "");
        await expect(listPage.entries.searchBox).toHaveValue("");

        await listPage.entries.searchFor("no such statutory words");
        await expect(listPage.entries.noMatches).toContainText("No sets match this search.");
        await expect(listPage.entries.status).toHaveText("No sets to show.");
        await capture("parameter-sets-no-matches", listPage.region);
      });
    });
  }

  test("keeps the list's address canonical", async ({ page, person, request }) => {
    const listPage = new ParameterSetsPage(page);
    await assignRoles(request, person, ["finance"]);
    const sets = await readParameterSets(page.request);
    const pageCount = Math.ceil(sets.length / setsPerPage);

    for (const { address, canonical } of [
      { address: `${parameterSetsPath}?page=1`, canonical: "" },
      { address: `${parameterSetsPath}?page=two`, canonical: "" },
      { address: `${parameterSetsPath}?q=%20GST%20`, canonical: "?q=GST" },
      {
        address: searchedListAddress(parameterSetsPath, { page: 99 }),
        canonical: pageCount > 1 ? `?page=${pageCount}` : "",
      },
    ]) {
      await listPage.goto(address);
      await expect(page, address).toHaveURL(
        (url) => url.pathname === parameterSetsPath && url.search === canonical,
      );
    }
  });
});

test.describe("statutory parameters for an administrator", () => {
  test.use({ persona: "administrator" });

  forEachTheme("show the entry under Compliance and the sets", async ({ page, capture }) => {
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
    await listPage.waitUntilInteractive();
    expect(await listPage.entries.rows()).toEqual(sets.slice(0, setsPerPage).map(setRow));
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
