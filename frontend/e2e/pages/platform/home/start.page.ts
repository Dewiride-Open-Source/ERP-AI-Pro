import { expect, type Locator, type Page } from "@playwright/test";

import { AppShell } from "../../shared/layout/app-shell.page";

export const homePath = "/";

export class StartPage {
  readonly shell: AppShell;
  readonly heading: Locator;
  readonly areas: Locator;
  readonly noAreas: Locator;

  constructor(private readonly page: Page) {
    this.shell = new AppShell(page);
    this.heading = page.getByRole("main").getByRole("heading", { level: 1 });
    this.areas = page.getByTestId("start-area");
    this.noAreas = page.getByTestId("start-no-areas");
  }

  async goto(): Promise<void> {
    await this.page.goto(homePath);
    await expect(this.heading).toBeVisible();
  }

  area(title: string): Locator {
    return this.areas.filter({ has: this.page.getByRole("heading", { name: title, exact: true }) });
  }

  openLink(title: string): Locator {
    return this.page.getByRole("link", { name: `Open ${title}`, exact: true });
  }
}
