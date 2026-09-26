import { expect, type Locator, type Page } from "@playwright/test";

export type UploadFile = { name: string; mimeType: string; buffer: Buffer };

export class AttachmentsPage {
  readonly heading: Locator;
  readonly uploadCard: Locator;
  readonly listCard: Locator;
  readonly dropZone: Locator;
  readonly chooseFile: Locator;
  readonly fileInput: Locator;
  readonly uploadStatus: Locator;
  readonly uploadError: Locator;
  readonly uploadProgress: Locator;
  readonly table: Locator;
  readonly rows: Locator;
  readonly empty: Locator;
  readonly unavailable: Locator;
  readonly deleteDialog: Locator;
  readonly confirmDelete: Locator;
  readonly cancelDelete: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { name: "Attachments", level: 1 });
    this.uploadCard = page.getByTestId("attachments-upload-card");
    this.listCard = page.getByTestId("attachments-list-card");
    this.dropZone = page.getByTestId("file-drop-zone");
    this.chooseFile = page.getByTestId("file-drop-zone-choose");
    this.fileInput = page.getByTestId("file-drop-zone-input");
    this.uploadStatus = page.getByTestId("upload-status");
    this.uploadError = page.getByTestId("upload-error");
    this.uploadProgress = page.getByTestId("upload-progress");
    this.table = page.getByTestId("attachments-table");
    this.rows = page.getByTestId("attachments-row");
    this.empty = page.getByTestId("attachments-empty");
    this.unavailable = page.getByTestId("attachments-unavailable");
    this.deleteDialog = page.getByTestId("attachment-delete-dialog");
    this.confirmDelete = page.getByTestId("attachment-delete-confirm");
    this.cancelDelete = page.getByTestId("attachment-delete-cancel");
  }

  async goto(): Promise<void> {
    await this.page.goto("/platform/attachments");
    await expect(this.heading).toBeVisible();
  }

  row(fileName: string): Locator {
    return this.rows.filter({
      has: this.page.getByTestId("attachments-file-name").getByText(fileName, { exact: true }),
    });
  }

  async upload(file: UploadFile): Promise<void> {
    await this.fileInput.setInputFiles(file);
  }
}
