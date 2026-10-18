import { expect, type Locator, type Page } from "@playwright/test";

export const orgChartPath = "/identity/users/org-chart";

export const orgChartApi = "/api/identity/users/org-chart";

export function orgChartAddress({
  department,
  on,
  page,
}: {
  readonly department?: string;
  readonly on?: string;
  readonly page?: number;
}): string {
  const search = new URLSearchParams();
  if (department !== undefined) search.set("department", department);
  if (on !== undefined) search.set("on", on);
  if (page !== undefined && page > 1) search.set("page", String(page));
  const query = search.toString();
  return query === "" ? orgChartPath : `${orgChartPath}?${query}`;
}

export class OrgChartPage {
  readonly heading: Locator;
  readonly card: Locator;
  readonly chartHeading: Locator;
  readonly filters: Locator;
  readonly department: Locator;
  readonly date: Locator;
  readonly dateTrigger: Locator;
  readonly show: Locator;
  readonly everyDepartment: Locator;
  readonly today: Locator;
  readonly status: Locator;
  readonly people: Locator;
  readonly expandAll: Locator;
  readonly collapseAll: Locator;
  readonly offChartNote: Locator;
  readonly empty: Locator;
  readonly pages: Locator;
  readonly pageLabel: Locator;
  readonly otherDateRefused: Locator;
  readonly unavailable: Locator;
  readonly departmentsUnavailable: Locator;

  constructor(private readonly page: Page) {
    const main = page.getByRole("main");
    this.heading = main.getByRole("heading", { name: "Organisation chart", level: 1 });
    this.card = page.getByTestId("org-chart-card");
    this.chartHeading = this.card.getByRole("heading", { level: 2 });
    this.filters = page.getByRole("search", { name: "Choose what the chart shows" });
    this.department = this.filters.getByRole("combobox", { name: "Department" });
    this.date = this.filters.getByRole("textbox", { name: "Date" });
    this.dateTrigger = this.filters.getByRole("button", { name: "Choose date", exact: true });
    this.show = this.filters.getByRole("button", { name: "Show", exact: true });
    this.everyDepartment = this.filters.getByRole("link", { name: "Show every department", exact: true });
    this.today = this.filters.getByRole("link", { name: "Show today", exact: true });
    this.status = page.getByTestId("org-chart-status");
    this.people = page.getByTestId("org-chart-people");
    this.expandAll = page.getByTestId("org-chart-expand-all");
    this.collapseAll = page.getByTestId("org-chart-collapse-all");
    this.offChartNote = page.getByTestId("org-chart-off-chart-note");
    this.empty = page.getByTestId("org-chart-empty");
    this.pages = page.getByRole("navigation", { name: "Organisation chart, pages" });
    this.pageLabel = page.getByTestId("org-chart-page");
    this.otherDateRefused = page.getByTestId("org-chart-other-date-refused");
    this.unavailable = page.getByTestId("org-chart-unavailable");
    this.departmentsUnavailable = page.getByTestId("org-chart-departments-unavailable");
  }

  async goto(address = orgChartPath): Promise<void> {
    await this.page.goto(address);
    await expect(this.heading).toBeVisible();
  }

  // The tree's buttons and the filter's fields are disabled in the server's HTML and mark themselves as rendering that state
  // until React hydrates them.
  async waitUntilInteractive(): Promise<void> {
    await expect(this.page.getByRole("main").locator("[data-hydrating]")).toHaveCount(0);
  }

  // A person's row holds their name, position and notes; the people who report to them follow in a list beside the row.
  row(name: string): Locator {
    return this.page
      .getByTestId("org-chart-row")
      .filter({ has: this.page.getByTestId("org-chart-name").getByText(name, { exact: true }) });
  }

  item(name: string): Locator {
    return this.row(name).locator("xpath=..");
  }

  reportsList(name: string): Locator {
    return this.item(name).locator(":scope > ul");
  }

  toggle(name: string): Locator {
    return this.row(name).getByRole("button", { name: `Show the people who report to ${name}`, exact: true });
  }

  pageLink(name: "First page" | "Previous page" | "Next page" | "Last page"): Locator {
    return this.pages.getByRole("link", { name, exact: true });
  }

  rootNames(): Promise<string[]> {
    return this.people
      .locator(":scope > li > [data-testid='org-chart-row'] [data-testid='org-chart-name']")
      .allInnerTexts();
  }

  async chooseDepartment(search: string, option: string): Promise<void> {
    await this.department.fill(search);
    const list = this.page.getByRole("listbox");
    await expect(list.getByRole("option")).toHaveCount(1);
    await expect(list.getByRole("option", { name: option, exact: true })).toBeVisible();
    await this.department.press("ArrowDown");
    await expect(list.getByRole("option", { name: option, exact: true })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    await this.department.press("Enter");
    await expect(list).toBeHidden();
    await expect(this.department).toHaveValue(option);
  }
}
