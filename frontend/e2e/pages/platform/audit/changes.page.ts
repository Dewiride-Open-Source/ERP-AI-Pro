import { expect, type Locator, type Page } from "@playwright/test";
import { readFile } from "node:fs/promises";

import { DataTableRegion } from "../../shared/lists/data-table.page";

export const auditTrailPath = "/platform/audit";

export const changesApi = "/api/platform/audit/changes";

export const exportApi = "/api/platform/audit/changes/export";

export type ExportedFile = { readonly name: string; readonly content: Buffer };

function savesDownloadsFromTheTestOrigin(page: Page): boolean {
  return !(process.platform === "win32" && page.context().browser()?.browserType().name() === "webkit");
}

export class AuditTrailPage {
  readonly heading: Locator;
  readonly changesHeading: Locator;
  readonly card: Locator;
  readonly feed: Locator;
  readonly feedItems: Locator;
  readonly list: DataTableRegion;
  readonly timeline: Locator;
  readonly days: Locator;
  readonly changes: Locator;
  readonly exportButton: Locator;
  readonly exportRefusal: Locator;
  readonly exportError: Locator;
  readonly unavailable: Locator;
  readonly empty: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { name: "Audit trail", level: 1 });
    this.changesHeading = page.getByRole("heading", { name: "Changes", level: 2 });
    this.card = page.getByTestId("audit-changes-card");
    this.feed = this.card.locator("[data-slot='list-feed']");
    this.feedItems = this.card.getByTestId("list-feed-items");
    this.list = new DataTableRegion(page, this.card);
    this.timeline = this.card.getByRole("list", { name: "Changes", exact: true });
    this.days = this.timeline.locator("[data-slot='timeline-group']");
    this.changes = this.card.getByTestId("audit-change");
    this.exportButton = this.card.getByTestId("audit-export");
    this.exportRefusal = this.card.getByTestId("audit-export-refusal");
    this.exportError = this.card.getByTestId("audit-export-error");
    this.unavailable = page.getByTestId("audit-unavailable");
    this.empty = page.getByTestId("audit-empty");
  }

  async goto(query = ""): Promise<void> {
    await this.page.goto(`${auditTrailPath}${query}`);
    await expect(this.heading).toBeVisible();
  }

  // The feed, like every list of the list kit, marks itself as rendering its server state until React hydrates it, and its
  // controls work only once it has.
  async waitUntilInteractive(): Promise<void> {
    await expect(this.feed).toBeVisible();
    await expect(this.feed).not.toHaveAttribute("data-hydrating");
  }

  day(heading: string): Locator {
    return this.days.filter({ has: this.page.getByRole("heading", { name: heading, exact: true }) });
  }

  change(summary: string): Locator {
    return this.changes.filter({
      has: this.page.getByTestId("audit-change-summary").getByText(summary, { exact: true }),
    });
  }

  summaries(): Promise<string[]> {
    return this.changes.getByTestId("audit-change-summary").allInnerTexts();
  }

  async download(): Promise<ExportedFile> {
    if (!savesDownloadsFromTheTestOrigin(this.page)) return this.fetchSavedExport();
    const started = this.page.waitForEvent("download");
    await this.exportButton.click();
    const download = await started;
    return { name: download.suggestedFilename(), content: await readFile(await download.path()) };
  }

  // Playwright's WebKit on Windows starts no download from an origin whose certificate it was told to accept, and it reports
  // the body of a text answer already decoded, without its byte order mark, so there the export the page fetched is fetched
  // once more with the context's cookies, from the same address, and named as the link the page clicked to save it.
  private async fetchSavedExport(): Promise<ExportedFile> {
    const saved = await this.page.evaluateHandle(() => {
      const names: string[] = [];
      document.addEventListener(
        "click",
        (event) => {
          if (event.target instanceof HTMLAnchorElement && event.target.hasAttribute("download")) {
            names.push(event.target.download);
          }
        },
        { capture: true },
      );
      return names;
    });
    const answered = this.page.waitForResponse((response) => new URL(response.url()).pathname === exportApi);
    await this.exportButton.click();
    const response = await answered;
    expect(response.status(), "the export the page fetched").toBe(200);
    await expect
      .poll(() => saved.evaluate((names) => names.length), { message: "files the page saved" })
      .toBe(1);
    const name = await saved.evaluate(([first]) => first ?? "");
    await saved.dispose();
    const again = await this.page.request.get(response.url());
    expect(again.status(), "the export fetched with the context's cookies").toBe(200);
    expect(again.headers()["content-disposition"] ?? "", "the export answers with a file").toMatch(
      /^attachment;/,
    );
    return { name, content: await again.body() };
  }
}
