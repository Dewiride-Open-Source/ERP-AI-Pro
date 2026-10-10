import { expect, type JSHandle, type Locator, type Page } from "@playwright/test";
import { readFile } from "node:fs/promises";

import type { FileUpload } from "../../../fixtures/files";
import { DataTableRegion } from "../../shared/lists/data-table.page";

export const attachmentsPath = "/platform/attachments";

export type DownloadedFile = { readonly name: string; readonly content: Buffer };

function savesDownloadsFromTheTestOrigin(page: Page): boolean {
  return !(process.platform === "win32" && page.context().browser()?.browserType().name() === "webkit");
}

export class AttachmentsPage {
  readonly heading: Locator;
  readonly listHeading: Locator;
  readonly uploadCard: Locator;
  readonly listCard: Locator;
  readonly dropZone: Locator;
  readonly chooseFile: Locator;
  readonly fileInput: Locator;
  readonly uploadStatus: Locator;
  readonly uploadAnnouncement: Locator;
  readonly uploadError: Locator;
  readonly uploadProgress: Locator;
  readonly list: DataTableRegion;
  readonly empty: Locator;
  readonly unavailable: Locator;
  readonly deleteDialog: Locator;
  readonly confirmDelete: Locator;
  readonly cancelDelete: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { name: "Attachments", level: 1 });
    this.listHeading = page.getByRole("heading", { name: "Stored files", level: 2 });
    this.uploadCard = page.getByTestId("attachments-upload-card");
    this.listCard = page.getByTestId("attachments-list-card");
    this.dropZone = page.getByTestId("file-drop-zone");
    this.chooseFile = page.getByTestId("file-drop-zone-choose");
    this.fileInput = page.getByTestId("file-drop-zone-input");
    this.uploadStatus = page.getByTestId("upload-status");
    this.uploadAnnouncement = this.uploadStatus.locator("[aria-live=polite]");
    this.uploadError = page.getByTestId("upload-error");
    this.uploadProgress = page.getByTestId("upload-progress");
    this.list = new DataTableRegion(page, this.listCard);
    this.empty = page.getByTestId("attachments-empty");
    this.unavailable = page.getByTestId("attachments-unavailable");
    this.deleteDialog = page.getByRole("alertdialog", { name: "Delete this attachment?" });
    this.confirmDelete = this.deleteDialog.getByRole("button", { name: "Delete", exact: true });
    this.cancelDelete = this.deleteDialog.getByRole("button", { name: "Keep it" });
  }

  async goto(query = ""): Promise<void> {
    await this.page.goto(`${attachmentsPath}${query}`);
    await expect(this.heading).toBeVisible();
  }

  // The list renders its rows twice, as a table and as cards, and shows one of them by the width of its card.
  row(fileName: string): Locator {
    return this.list.tableRows
      .or(this.list.cards)
      .filter({ visible: true })
      .filter({ has: this.page.getByTestId("attachments-file-name").getByText(fileName, { exact: true }) });
  }

  async download(row: Locator): Promise<DownloadedFile> {
    const button = row.getByTestId("attachment-download");
    if (!savesDownloadsFromTheTestOrigin(this.page)) return this.fetchOpenedDownload(button);
    const started = this.page.waitForEvent("download");
    await button.click();
    const download = await started;
    return { name: download.suggestedFilename(), content: await readFile(await download.path()) };
  }

  async upload(file: FileUpload): Promise<void> {
    await this.untilDropZoneTakesFiles();
    await this.fileInput.setInputFiles(file);
  }

  async chooseAndUpload(file: FileUpload): Promise<void> {
    const chooser = this.page.waitForEvent("filechooser");
    await this.chooseFile.click();
    await (await chooser).setFiles(file);
  }

  async transferOf(file: FileUpload): Promise<JSHandle<DataTransfer>> {
    return this.page.evaluateHandle(
      ({ name, mimeType, bytes }) => {
        const transfer = new DataTransfer();
        transfer.items.add(new File([new Uint8Array(bytes)], name, { type: mimeType }));
        return transfer;
      },
      { name: file.name, mimeType: file.mimeType, bytes: [...file.buffer] },
    );
  }

  async dragOver(dataTransfer: JSHandle<DataTransfer>): Promise<void> {
    await this.dispatchDrag(["dragenter", "dragover"], dataTransfer);
  }

  async drop(dataTransfer: JSHandle<DataTransfer>): Promise<void> {
    await this.dispatchDrag(["drop"], dataTransfer);
    await dataTransfer.dispose();
  }

  private async fetchOpenedDownload(button: Locator): Promise<DownloadedFile> {
    const opened = await this.page.evaluateHandle(() => {
      const links: { href: string; name: string }[] = [];
      document.addEventListener(
        "click",
        (event) => {
          if (event.target instanceof HTMLAnchorElement && event.target.hasAttribute("download")) {
            links.push({ href: event.target.href, name: event.target.download });
          }
        },
        { capture: true },
      );
      return links;
    });
    await button.click();
    await expect
      .poll(() => opened.evaluate((links) => links.length), { message: "downloads the page started" })
      .toBe(1);
    const { href, name } = await opened.evaluate(([link]) => link ?? { href: "", name: "" });
    await opened.dispose();
    const response = await this.page.request.get(href);
    expect(response.status(), "the download link the page opened").toBe(200);
    const disposition = response.headers()["content-disposition"] ?? "";
    expect(disposition, "the download link answers with the file as an attachment").toMatch(/^attachment;/);
    expect(disposition, "the download link names the file").toContain(name);
    return { name, content: await response.body() };
  }

  private async untilDropZoneTakesFiles(): Promise<void> {
    // "Choose a file" is disabled until the drop zone has hydrated and while it uploads a file, the times it ignores one.
    await expect(this.chooseFile).toBeEnabled();
  }

  private async dispatchDrag(types: readonly string[], dataTransfer: JSHandle<DataTransfer>): Promise<void> {
    await this.untilDropZoneTakesFiles();
    await this.dropZone.evaluate(
      (zone, { types, dataTransfer }) => {
        for (const type of types) {
          zone.dispatchEvent(new DragEvent(type, { bubbles: true, cancelable: true, dataTransfer }));
        }
      },
      { types, dataTransfer },
    );
  }
}
