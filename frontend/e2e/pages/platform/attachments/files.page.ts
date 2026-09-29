import { expect, type JSHandle, type Locator, type Page } from "@playwright/test";

import type { FileUpload } from "../../../fixtures/files";
import { DataTableRegion } from "../../shared/lists/data-table.page";

export const attachmentsPath = "/platform/attachments";

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

  async upload(file: FileUpload): Promise<void> {
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

  private async dispatchDrag(types: readonly string[], dataTransfer: JSHandle<DataTransfer>): Promise<void> {
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
