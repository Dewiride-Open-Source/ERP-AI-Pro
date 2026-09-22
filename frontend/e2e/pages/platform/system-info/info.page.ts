import { expect, type Locator, type Page } from "@playwright/test";

export class SystemInfoPage {
  readonly heading: Locator;
  readonly card: Locator;
  readonly application: Locator;
  readonly version: Locator;
  readonly started: Locator;
  readonly uptime: Locator;
  readonly refresh: Locator;
  readonly unavailable: Locator;
  readonly startupsCard: Locator;
  readonly startupsTable: Locator;
  readonly startupsRows: Locator;
  readonly startupsVersion: Locator;
  readonly startupsEmpty: Locator;
  readonly startupsUnavailable: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { name: "System information" });
    this.card = page.getByTestId("system-info-card");
    this.application = page.getByTestId("system-info-application");
    this.version = page.getByTestId("system-info-version");
    this.started = page.getByTestId("system-info-started");
    this.uptime = page.getByTestId("system-info-uptime");
    this.refresh = page.getByTestId("system-info-refresh");
    this.unavailable = page.getByTestId("system-info-unavailable");
    this.startupsCard = page.getByTestId("recent-startups-card");
    this.startupsTable = page.getByTestId("recent-startups-table");
    this.startupsRows = page.getByTestId("recent-startups-row");
    this.startupsVersion = this.startupsRows.first().getByTestId("recent-startups-version");
    this.startupsEmpty = page.getByTestId("recent-startups-empty");
    this.startupsUnavailable = page.getByTestId("recent-startups-unavailable");
  }

  async goto(): Promise<void> {
    await this.page.goto("/platform/system-info");
    await expect(this.heading).toBeVisible();
  }
}
