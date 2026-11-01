import { expect, type Locator, type Page } from "@playwright/test";

import { DataTableRegion } from "../../shared/lists/data-table.page";

export const jobsPath = "/platform/jobs";

export const jobsApi = "/api/platform/jobs";

export const jobRunsApi = "/api/platform/jobs/runs";

export function jobRunPath(id: string): string {
  return `${jobsPath}/runs/${id}`;
}

export function jobRunApi(id: string): string {
  return `${jobRunsApi}/${id}`;
}

export class JobsPage {
  readonly heading: Locator;
  readonly jobsCard: Locator;
  readonly jobs: Locator;
  readonly jobsUnavailable: Locator;
  readonly jobsEmpty: Locator;
  readonly runsCard: Locator;
  readonly runsHeading: Locator;
  readonly runsUnavailable: Locator;
  readonly runsEmpty: Locator;
  readonly list: DataTableRegion;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { name: "Background jobs", level: 1 });
    this.jobsCard = page.getByTestId("jobs-card");
    this.jobs = this.jobsCard.getByTestId("job");
    this.jobsUnavailable = page.getByTestId("jobs-unavailable");
    this.jobsEmpty = page.getByTestId("jobs-empty");
    this.runsCard = page.getByTestId("job-runs-card");
    this.runsHeading = this.runsCard.getByRole("heading", { name: "Runs", level: 2 });
    this.runsUnavailable = page.getByTestId("job-runs-unavailable");
    this.runsEmpty = page.getByTestId("job-runs-empty");
    this.list = new DataTableRegion(page, this.runsCard);
  }

  async goto(query = ""): Promise<void> {
    await this.page.goto(`${jobsPath}${query}`);
    await expect(this.heading).toBeVisible();
  }

  // The run actions, like the list's controls, are disabled in the server's HTML and mark themselves as rendering that state
  // until React hydrates them.
  async waitUntilInteractive(): Promise<void> {
    await this.list.waitUntilInteractive();
    await expect(this.runsCard.locator("[data-hydrating]")).toHaveCount(0);
  }

  job(title: string): Locator {
    return this.jobs.filter({ has: this.page.getByRole("heading", { name: title, exact: true, level: 3 }) });
  }

  // A run's key is unique among the runs of its job, and each spec's keys carry its own prefix, so a row is found by its key
  // whether the list shows a table or, on a narrow screen, cards.
  async run(key: string): Promise<Locator> {
    return (await this.list.rows()).filter({
      has: this.page.getByTestId("job-run-key").getByText(key, { exact: true }),
    });
  }

  async keys(): Promise<string[]> {
    return (await this.list.rows()).getByTestId("job-run-key").allInnerTexts();
  }

  status(run: Locator): Locator {
    return run.getByTestId("job-run-status");
  }

  rerun(run: Locator): Locator {
    return run.getByTestId("job-run-rerun");
  }

  cancel(run: Locator): Locator {
    return run.getByTestId("job-run-cancel");
  }

  rerunError(run: Locator): Locator {
    return run.getByTestId("job-run-rerun-error");
  }

  cancelError(run: Locator): Locator {
    return run.getByTestId("job-run-cancel-error");
  }

  confirmation(title: string): Locator {
    return this.page.getByRole("alertdialog", { name: title });
  }
}
