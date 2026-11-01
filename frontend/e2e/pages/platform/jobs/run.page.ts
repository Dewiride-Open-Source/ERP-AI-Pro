import { expect, type Locator, type Page } from "@playwright/test";

import { jobRunPath } from "./runs.page";

export class JobRunPage {
  readonly heading: Locator;
  readonly header: Locator;
  readonly statusLine: Locator;
  readonly actions: Locator;
  readonly rerun: Locator;
  readonly cancel: Locator;
  readonly rerunError: Locator;
  readonly cancelError: Locator;
  readonly details: Locator;
  readonly history: Locator;
  readonly timeline: Locator;
  readonly events: Locator;
  readonly unavailable: Locator;

  constructor(private readonly page: Page) {
    const main = page.getByRole("main");
    this.heading = main.getByRole("heading", { level: 1 });
    this.header = main.locator("header").first();
    this.statusLine = page.getByTestId("job-run-status-line");
    this.actions = page.getByTestId("job-run-actions");
    this.rerun = page.getByTestId("job-run-rerun");
    this.cancel = page.getByTestId("job-run-cancel");
    this.rerunError = page.getByTestId("job-run-rerun-error");
    this.cancelError = page.getByTestId("job-run-cancel-error");
    this.details = page.getByTestId("job-run-details");
    this.history = page.getByTestId("job-run-history");
    this.timeline = this.history.getByRole("list", { name: "History", exact: true });
    this.events = this.history.getByTestId("job-run-event");
    this.unavailable = page.getByTestId("job-run-unavailable");
  }

  async goto(id: string): Promise<void> {
    await this.page.goto(jobRunPath(id));
    await expect(this.heading).toBeVisible();
  }

  // The run's buttons open dialogs, so they are disabled in the server's HTML and mark themselves as rendering that state
  // until React hydrates them.
  async waitUntilInteractive(): Promise<void> {
    await expect(this.page.getByRole("main").locator("[data-hydrating]")).toHaveCount(0);
  }

  detail(label: string): Locator {
    return this.details
      .locator("dl > div")
      .filter({ has: this.page.getByRole("term").getByText(label, { exact: true }) })
      .getByRole("definition");
  }

  summaries(): Promise<string[]> {
    return this.events.getByTestId("job-run-event-summary").allInnerTexts();
  }

  confirmation(title: string): Locator {
    return this.page.getByRole("alertdialog", { name: title });
  }
}
