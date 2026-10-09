import type { Page, Route } from "@playwright/test";

import { expect, forEachTheme, tabOntoLink, test } from "../../../fixtures/test";
import { DataTableDemoPage, dataTablePath } from "../../../pages/platform/design/data-table.page";

function search(page: Page): URLSearchParams {
  return new URL(page.url()).searchParams;
}

async function expectAddress(page: Page, expected: string): Promise<void> {
  await expect(page).toHaveURL((url) => url.pathname === dataTablePath && url.search === expected);
}

// A list change is a client navigation whose React Server Components request carries the RSC header; holding it keeps the
// previous rows on screen for as long as the test needs to look at them.
async function holdListNavigation(page: Page): Promise<() => void> {
  let release: () => void = () => undefined;
  const released = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route(
    (url) => url.pathname === dataTablePath,
    async (route: Route) => {
      if (route.request().headers()["rsc"] !== "1") {
        await route.fallback();
        return;
      }
      await released;
      await route.fallback();
    },
  );
  return release;
}

function isClientBundle(url: URL): boolean {
  return url.pathname.startsWith("/_next/static/") && url.pathname.endsWith(".js");
}

async function controlPlacements(page: Page): Promise<string[]> {
  return page.evaluate(async () => {
    await document.fonts.ready;
    await Promise.all(
      document
        .getAnimations()
        .filter((animation) => animation.effect?.getComputedTiming().iterations !== Infinity)
        .map((animation) => animation.finished),
    );
    const seen = new Map<string, number>();
    return [...document.querySelectorAll("main a, main button, main input")]
      .filter(
        (control) => control.getClientRects().length > 0 && control.closest("[aria-hidden='true']") === null,
      )
      .map((control) => {
        const name = control.getAttribute("aria-label") ?? control.textContent?.trim() ?? "";
        const key = `${control.tagName.toLowerCase()} "${name}"`;
        const count = (seen.get(key) ?? 0) + 1;
        seen.set(key, count);
        const box = control.getBoundingClientRect();
        return `${key} #${count} at ${Math.round(box.x + window.scrollX)},${Math.round(box.y + window.scrollY)} sized ${Math.round(box.width)}x${Math.round(box.height)}`;
      });
  });
}

test.describe("data table", () => {
  forEachTheme(
    "lists the bills by due date, earliest first, with every control",
    async ({ page, capture }) => {
      const demo = new DataTableDemoPage(page);
      const { bills } = demo;
      await demo.goto();

      await expect(bills.root).not.toHaveAttribute("data-hydrating");
      await expect(page).toHaveTitle(/Data table · ERP-AI-Pro/);
      await expect(bills.status).toHaveText("Showing 1–10 of 64 bills. Sorted by Due date, earliest first.");
      await expect(bills.pageLabel).toHaveText("Page 1 of 7");
      expect((await bills.titles()).slice(0, 3)).toEqual([
        "PB/2026-27/0001",
        "PB/2026-27/0004",
        "PB/2026-27/0002",
      ]);
      await expect(bills.previousPage).toHaveAttribute("aria-disabled", "true");
      await expect(bills.filters).toMatchAriaSnapshot(`
      - search "Filter purchase bills":
        - group:
          - text: Supplier
          - group:
            - group
            - searchbox "Supplier"
        - group:
          - text: Status
          - button "Status Any"
        - group:
          - text: Due between From
          - group:
            - textbox "Due between From"
          - text: To
          - group:
            - textbox "Due between To"
          - button "Choose dates"
        - button "Apply filters"
    `);

      if (await bills.showsCards()) {
        await expect(bills.table).toBeHidden();
        await expect(bills.cards.first()).toMatchAriaSnapshot(`
        - listitem:
          - checkbox "Select PB/2026-27/0001"
          - text: PB/2026-27/0001
          - term: Supplier
          - definition: Kaveri Traders
          - term: State
          - definition: Karnataka
          - term: Bill date
          - definition: 01-04-2026
          - term: Due date
          - definition: 16-04-2026
          - term: Amount
          - definition: ₹1,500.00
          - term: Status
          - definition: Draft
      `);
      } else {
        await expect(bills.cardList).toBeHidden();
        await expect(bills.columnHeader("Due date")).toHaveAttribute("aria-sort", "ascending");
        await expect(bills.columnHeader("Amount")).not.toHaveAttribute("aria-sort");
        await expect(bills.table.getByRole("row").first()).toMatchAriaSnapshot(`
        - row:
          - columnheader "Select all rows on this page":
            - checkbox "Select all rows on this page"
          - columnheader "Bill":
            - button "Bill"
          - columnheader "Supplier":
            - button "Supplier"
          - columnheader "State"
          - columnheader "Bill date":
            - button "Bill date"
          - columnheader "Due date":
            - button "Due date"
          - columnheader "Amount":
            - button "Amount"
          - columnheader "Status"
      `);
        await expect(bills.tableRows.first()).toMatchAriaSnapshot(`
        - row:
          - cell "Select PB/2026-27/0001":
            - checkbox "Select PB/2026-27/0001"
          - rowheader "PB/2026-27/0001"
          - cell "Kaveri Traders"
          - cell "Karnataka"
          - cell "01-04-2026"
          - cell "16-04-2026"
          - cell "₹1,500.00"
          - cell "Draft"
      `);
      }
      await capture("data-table-bills", page.getByTestId("purchase-bills-card"));
    },
  );

  test("sorts on the server from the column headers and keeps the view in the address", async ({ page }) => {
    const demo = new DataTableDemoPage(page);
    const { bills } = demo;
    await demo.goto();

    await bills.sortBy("Amount", "Amount, highest first");
    await expectAddress(page, "?sort=amount%3Adesc");
    await expect(bills.status).toHaveText("Showing 1–10 of 64 bills. Sorted by Amount, highest first.");
    await expect.poll(() => bills.titles()).toEqual(expect.arrayContaining(["PB/2026-27/0062"]));
    expect((await bills.titles()).slice(0, 2)).toEqual(["PB/2026-27/0062", "PB/2026-27/0061"]);

    await bills.sortBy("Amount", "Amount, lowest first");
    await expectAddress(page, "?sort=amount%3Aasc");
    await expect(bills.status).toContainText("Sorted by Amount, lowest first.");
    expect((await bills.titles())[0]).toBe("PB/2026-27/0001");

    await page.goBack();
    await expectAddress(page, "?sort=amount%3Adesc");
    await expect(bills.status).toContainText("Sorted by Amount, highest first.");
    expect((await bills.titles())[0]).toBe("PB/2026-27/0062");

    if (!(await bills.showsCards())) {
      await expect(bills.columnHeader("Amount")).toHaveAttribute("aria-sort", "descending");
      await expect(bills.columnHeader("Due date")).not.toHaveAttribute("aria-sort");
      const supplier = bills.sortButton("Supplier");
      await supplier.focus();
      await page.keyboard.press("Enter");
      await expectAddress(page, "?sort=supplier%3Aasc");
      await expect(bills.columnHeader("Supplier")).toHaveAttribute("aria-sort", "ascending");
      await expect(supplier).toBeFocused();
      expect((await bills.titles()).slice(0, 2)).toEqual(["PB/2026-27/0007", "PB/2026-27/0019"]);
    }
  });

  test("pages with links, keeps focus on them and changes the page size", async ({ page }) => {
    const demo = new DataTableDemoPage(page);
    const { bills } = demo;
    await demo.goto();

    await tabOntoLink(page, bills.nextPage);
    await page.keyboard.press("Enter");
    await expectAddress(page, "?page=2");
    await expect(bills.status).toContainText("Showing 11–20 of 64 bills.");
    await expect(bills.pageLabel).toHaveText("Page 2 of 7");
    await expect(bills.nextPage).toBeFocused();

    await tabOntoLink(page, bills.lastPage);
    await page.keyboard.press("Enter");
    await expectAddress(page, "?page=7");
    await expect(bills.status).toContainText("Showing 61–64 of 64 bills.");
    await expect(bills.lastPage).toBeFocused();
    await expect(bills.lastPage).toHaveAttribute("aria-disabled", "true");
    await expect(bills.nextPage).toHaveAttribute("aria-disabled", "true");
    const listRequests: URL[] = [];
    page.on("request", (request) => {
      const url = new URL(request.url());
      if (request.headers()["rsc"] === "1" && url.pathname === dataTablePath) listRequests.push(url);
    });
    await page.keyboard.press("Enter");
    await expect(bills.lastPage).toBeFocused();

    await tabOntoLink(page, bills.firstPage);
    await page.keyboard.press("Enter");
    await expectAddress(page, "");
    await expect(bills.pageLabel).toHaveText("Page 1 of 7");
    await expect(bills.firstPage).toBeFocused();
    await expect(bills.firstPage).toHaveAttribute("aria-disabled", "true");
    await expect(bills.previousPage).toHaveAttribute("aria-disabled", "true");
    expect(
      listRequests.filter((url) => url.searchParams.get("page") === "7"),
      "requests sent by the unavailable last-page link",
    ).toEqual([]);

    await bills.nextPage.click();
    await expectAddress(page, "?page=2");
    await bills.previousPage.click();
    await expectAddress(page, "");
    await bills.nextPage.click();
    await expectAddress(page, "?page=2");
    await bills.choosePageSize(20);
    await expectAddress(page, "?size=20");
    await expect(bills.status).toContainText("Showing 1–20 of 64 bills.");
    await expect(bills.pageLabel).toHaveText("Page 1 of 4");
    await expect(await bills.rows()).toHaveCount(20);
  });

  forEachTheme(
    "filters by supplier, status and due date, and clears the filters",
    async ({ page, capture }) => {
      const demo = new DataTableDemoPage(page);
      const { bills } = demo;
      await demo.goto("?page=3");

      const supplier = bills.textFilter("Supplier");
      await supplier.fill("ganesh");
      await supplier.press("Enter");
      await expectAddress(page, "?supplier=ganesh");
      await expect(bills.status).toContainText("Showing all 5 bills.");
      await expect(supplier).toBeFocused();
      await expect(supplier).toHaveValue("ganesh");

      await bills.optionsFilter("Status").click();
      await expect(page.getByRole("menu")).toBeVisible();
      await expect(page.locator("body"), "the page keeps its scrollbar").not.toHaveAttribute(
        "data-scroll-locked",
      );
      await page.keyboard.press("Escape");
      await expect(page.getByRole("menu")).toHaveCount(0);
      await bills.chooseOptions("Status", ["Paid"]);
      await expect(bills.optionsFilter("Status")).toHaveAccessibleName("Status Paid");
      await bills.applyFilters.click();
      await expectAddress(page, "?supplier=ganesh&status=paid");
      await expect(bills.status).toContainText("Showing all 5 bills.");
      for (const title of await bills.titles()) expect(title).toMatch(/^PB\/2026-27\/\d{4}$/);
      await expect(await bills.row("Shree Ganesh & Sons")).toHaveCount(5);

      await supplier.fill("");
      await bills.rangeStart("Due between").fill("01-08-2026");
      await bills.rangeTrigger.focus();
      await page.keyboard.press("Enter");
      const calendar = page.getByRole("dialog", { name: "Choose dates", exact: true });
      await expect(calendar).toBeVisible();
      await expect(calendar.getByRole("button", { name: /\b1 August 2026/ })).toBeFocused();
      for (const key of ["ArrowDown", "ArrowDown", "ArrowDown", "ArrowDown", "ArrowRight", "ArrowRight"]) {
        await page.keyboard.press(key);
      }
      await expect(
        calendar.getByRole("grid", { name: /^August/ }).getByRole("button", { name: /\b31 August 2026/ }),
      ).toBeFocused();
      await page.keyboard.press("Enter");
      await expect(calendar).toBeHidden();
      await expect(bills.rangeEnd("Due between")).toHaveValue("31-08-2026");
      await bills.applyFilters.click();
      await expectAddress(page, "?status=paid&dueFrom=2026-08-01&dueTo=2026-08-31");
      await bills.optionsFilter("Status").click();
      await page.getByRole("menuitem", { name: "Clear choices" }).click();
      await bills.applyFilters.click();
      await expectAddress(page, "?dueFrom=2026-08-01&dueTo=2026-08-31");
      await expect(bills.status).toContainText("Showing 1–10 of 12 bills.");
      await capture("data-table-filtered", page.getByTestId("purchase-bills-card"));

      await bills.filters.getByRole("link", { name: "Clear filters" }).click();
      await expectAddress(page, "");
      await expect(bills.status).toContainText("Showing 1–10 of 64 bills.");
      await expect(bills.textFilter("Supplier")).toBeFocused();
      await expect(bills.rangeStart("Due between")).toHaveValue("");
    },
  );

  test("refuses a due date range it cannot use without leaving the page", async ({ page, capture }) => {
    const demo = new DataTableDemoPage(page);
    const { bills } = demo;
    await demo.goto("?supplier=kaveri");
    const from = bills.rangeStart("Due between");
    const to = bills.rangeEnd("Due between");

    await from.fill("31-02-2026");
    await from.press("Enter");
    await expect(from).toBeFocused();
    await expect(from).toHaveAttribute("aria-invalid", "true");
    await expect(to).not.toHaveAttribute("aria-invalid");
    await expect(bills.filters).toContainText("Enter a real date as day-month-year, for example 31-03-2026.");
    await expectAddress(page, "?supplier=kaveri");

    await from.fill("10-08-2026");
    await to.fill("01-08-2026");
    await to.blur();
    await expect(to).not.toHaveAttribute("aria-invalid");
    await expect(bills.filters).not.toContainText("The end date is before the start date.");
    await bills.applyFilters.click();
    await expect(to).toBeFocused();
    await expect(to).toHaveAttribute("aria-invalid", "true");
    await expect(bills.filters).toContainText("The end date is before the start date.");
    await expectAddress(page, "?supplier=kaveri");
    await capture("data-table-refused-range", bills.filters);
  });

  test("says when no bill matches and clears the filters from there", async ({ page, capture }) => {
    const demo = new DataTableDemoPage(page);
    const { bills } = demo;
    await demo.goto("?supplier=kaveri&status=paid");

    await expect(bills.noMatches).toBeVisible();
    await expect(bills.noMatches).toContainText("No bills match these filters.");
    await expect(bills.status).toHaveText("No bills to show.");
    await expect(bills.table).toHaveCount(0);
    await expect(bills.pageSize).toHaveCount(0);
    await capture("data-table-no-matches", page.getByTestId("purchase-bills-card"));

    await bills.noMatches.getByRole("link", { name: "Clear filters" }).click();
    await expectAddress(page, "");
    await expect(bills.status).toContainText("Showing 1–10 of 64 bills.");
    await expect(bills.textFilter("Supplier")).toBeFocused();
    await expect(bills.textFilter("Supplier")).toHaveValue("");
  });

  test("hides and shows columns and keeps them in the address", async ({ page, capture }) => {
    const demo = new DataTableDemoPage(page);
    const { bills } = demo;
    await demo.goto("?page=2");

    await bills.columnsButton.click();
    await expect(page.getByRole("menu")).toMatchAriaSnapshot(`
      - menu:
        - group:
          - text: Show columns
          - menuitemcheckbox "Supplier" [checked]
          - menuitemcheckbox "State" [checked]
          - menuitemcheckbox "Bill date" [checked]
          - menuitemcheckbox "Due date" [checked]
          - menuitemcheckbox "Amount" [checked]
          - menuitemcheckbox "Status" [checked]
    `);
    await expect(page.locator("body"), "the page keeps its scrollbar").not.toHaveAttribute(
      "data-scroll-locked",
    );
    await capture("data-table-columns-menu");
    await page.keyboard.press("Escape");
    await expect(page.getByRole("menu")).toHaveCount(0);
    await expect(bills.columnsButton).toBeFocused();

    await bills.setColumnVisible("State", false);
    await bills.setColumnVisible("Bill date", false);
    await expectAddress(page, "?page=2&hide=state%2CbillDate");
    const cards = await bills.showsCards();
    if (cards)
      await expect(bills.cards.first().getByRole("term")).toHaveText([
        "Supplier",
        "Due date",
        "Amount",
        "Status",
      ]);
    else await expect(bills.table.getByRole("columnheader")).toHaveCount(6);

    await page.reload();
    await expect(bills.status).toContainText("Showing 11–20 of 64 bills.");
    if (cards) await expect(bills.cards.first().getByRole("term")).toHaveCount(4);
    else await expect(bills.columnHeader("State")).toHaveCount(0);

    await bills.nextPage.click();
    await expectAddress(page, "?page=3&hide=state%2CbillDate");
    await bills.setColumnVisible("State", true);
    await bills.setColumnVisible("Bill date", true);
    await expectAddress(page, "?page=3");

    await bills.setColumnVisible("Due date", false);
    await expectAddress(page, "?page=3&hide=dueDate");
    await expect(bills.status).toContainText("Sorted by Due date, earliest first.");
    if (cards) await expect(bills.sortSelect).toHaveText("Due date, earliest first");
    await bills.setColumnVisible("Due date", true);
    await expectAddress(page, "?page=3");
  });

  test("selects rows on a page and across pages, and clears the selection", async ({ page }) => {
    const demo = new DataTableDemoPage(page);
    const { bills } = demo;
    await demo.goto();
    const region = (await bills.showsCards()) ? bills.cardList : bills.table;
    const all = bills.container.getByRole("checkbox", { name: "Select all rows on this page" });

    await region.getByRole("checkbox", { name: "Select PB/2026-27/0001" }).click();
    await expect(bills.selection).toHaveText("1 bill selected");
    await expect(all).toHaveAttribute("aria-checked", "mixed");
    await all.click();
    await expect(bills.selection).toHaveText("10 bills selected");
    await expect(all).toHaveAttribute("aria-checked", "true");
    await expect((await bills.rows()).first()).toHaveAttribute("data-state", "selected");

    await bills.nextPage.click();
    await expectAddress(page, "?page=2");
    await expect(bills.pageLabel).toHaveText("Page 2 of 7");
    await expect(all).toHaveAttribute("aria-checked", "false");
    const [secondPageFirst = ""] = await bills.titles();
    await region.getByRole("checkbox", { name: `Select ${secondPageFirst}` }).click();
    await expect(bills.selection).toHaveText("11 bills selected");
    await expect(all).toHaveAttribute("aria-checked", "mixed");

    await bills.container.getByRole("button", { name: "Clear selection" }).click();
    await expect(bills.selection).toHaveText("");
    await expect(region).toBeFocused();

    await region.getByRole("checkbox", { name: `Select ${secondPageFirst}` }).click();
    await expect(bills.selection).toHaveText("1 bill selected");
    await bills.textFilter("Supplier").fill("a");
    await bills.textFilter("Supplier").press("Enter");
    await expectAddress(page, "?supplier=a");
    await expect(bills.selection).toHaveText("");
    await bills.filters.getByRole("link", { name: "Clear filters" }).click();
    await expectAddress(page, "");
    await expect(bills.status).toContainText("Showing 1–10 of 64 bills.");
    await expect(bills.selection).toHaveText("");
  });

  test("keeps every control in place when the page becomes interactive", async ({ page }) => {
    const demo = new DataTableDemoPage(page);
    await demo.goto("?page=2");
    const interactive = await controlPlacements(page);

    await page.route(isClientBundle, (route) => route.fulfill({ contentType: "text/javascript", body: "" }));
    await page.reload();
    await expect(demo.bills.columnsButton).toBeDisabled();
    expect(await controlPlacements(page)).toEqual(interactive);
  });

  test.describe("before the page is interactive", () => {
    test.use({ javaScriptEnabled: false });

    test("marks the list as still loading and keeps its controls disabled", async ({ page }) => {
      const demo = new DataTableDemoPage(page);
      await page.goto(dataTablePath);

      await expect(demo.heading).toBeVisible();
      await expect(demo.bills.root).toHaveAttribute("data-hydrating", "");
      await expect(demo.bills.columnsButton).toBeDisabled();
    });
  });

  test.describe("from an address typed by hand", () => {
    test("brings an address it cannot show back to one it can", async ({ page }) => {
      const demo = new DataTableDemoPage(page);
      await demo.goto("?page=abc&size=7&sort=state%3Aasc&tab=open");
      await expectAddress(page, "");

      await demo.goto("?page=99");
      await expectAddress(page, "?page=7");

      await demo.goto("?status=cancelled&status=paid&dueFrom=2026-09-31&supplier=%20ganesh%20");
      await expectAddress(page, "?supplier=ganesh&status=paid");
      await expect(demo.bills.status).toContainText("Showing all 5 bills.");
      expect(search(page).getAll("status")).toEqual(["paid"]);
    });
  });

  test("keeps the rows on screen, marked busy, until the next page arrives", async ({ page }) => {
    const demo = new DataTableDemoPage(page);
    const { bills } = demo;
    await demo.goto();
    const region = (await bills.showsCards()) ? bills.cardList : bills.table;

    const release = await holdListNavigation(page);
    await bills.nextPage.click();
    await expect(bills.status).toHaveText("Loading…");
    await expect(region).toHaveAttribute("aria-busy", "true");
    expect((await bills.titles())[0]).toBe("PB/2026-27/0001");
    release();

    await expectAddress(page, "?page=2");
    await expect(bills.status).toContainText("Showing 11–20 of 64 bills.");
    await expect(region).not.toHaveAttribute("aria-busy");
  });
});
