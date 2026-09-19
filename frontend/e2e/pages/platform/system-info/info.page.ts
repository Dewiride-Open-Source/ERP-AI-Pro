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

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { name: "System information" });
    this.card = page.getByTestId("system-info-card");
    this.application = page.getByTestId("system-info-application");
    this.version = page.getByTestId("system-info-version");
    this.started = page.getByTestId("system-info-started");
    this.uptime = page.getByTestId("system-info-uptime");
    this.refresh = page.getByTestId("system-info-refresh");
    this.unavailable = page.getByTestId("system-info-unavailable");
  }

  async goto(): Promise<void> {
    await this.page.goto("/platform/system-info");
    await expect(this.heading).toBeVisible();
  }
}
