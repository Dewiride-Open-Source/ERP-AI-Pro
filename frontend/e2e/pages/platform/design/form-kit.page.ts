import { expect, type JSHandle, type Locator, type Page, type Request } from "@playwright/test";

import type { FileUpload } from "../../../fixtures/files";

export const formKitPath = "/design/form-kit";

export type ServerAnswer =
  | "Accept the supplier"
  | "Refuse the legal name"
  | "Refuse several nested details"
  | "Report the supplier as already registered"
  | "Fail with a server error"
  | "Do not respond (API unreachable)";

export type SupplierDetails = {
  legalName: string;
  gstin: string;
  pan: string;
  ifsc: string;
  state: string;
  categorySearch: string;
  category: string;
  openingBalance: string;
  agreementStart: string;
  validityFrom: string;
  validityTo: string;
};

// A Server Function call is a POST to the page that renders the form.
export function isSaveRequest(request: Request): boolean {
  return request.method() === "POST" && new URL(request.url()).pathname === formKitPath;
}

export const validSupplier: SupplierDetails = {
  legalName: "Globex Cloud Services Private Limited",
  gstin: "29 aabcg 1234k 1z5",
  pan: "aabcg1234k",
  ifsc: "hdfc0001234",
  state: "Karnataka",
  categorySearch: "cloud",
  category: "Cloud hosting",
  openingBalance: "125000.5",
  agreementStart: "01-04-2026",
  validityFrom: "01-04-2026",
  validityTo: "31-03-2027",
};

export class FormKitPage {
  readonly heading: Locator;
  readonly form: Locator;
  readonly summary: Locator;
  readonly legalName: Locator;
  readonly gstin: Locator;
  readonly pan: Locator;
  readonly ifsc: Locator;
  readonly state: Locator;
  readonly category: Locator;
  readonly openingBalance: Locator;
  readonly agreementStart: Locator;
  readonly agreementStartTrigger: Locator;
  readonly validityFrom: Locator;
  readonly validityTo: Locator;
  readonly validityTrigger: Locator;
  readonly document: Locator;
  readonly dropZone: Locator;
  readonly chooseFile: Locator;
  readonly documentStatus: Locator;
  readonly documentAnnouncement: Locator;
  readonly documentError: Locator;
  readonly removeDocument: Locator;
  readonly serverAnswer: Locator;
  readonly save: Locator;
  readonly outcome: Locator;
  readonly idempotencyKey: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { name: "Form kit", level: 1 });
    this.form = page.getByRole("form", { name: "Register a supplier (example)" });
    this.summary = this.form.locator("[data-slot='alert']");
    this.legalName = this.form.getByRole("textbox", { name: "Legal name" });
    this.gstin = this.form.getByRole("textbox", { name: "GSTIN" });
    this.pan = this.form.getByRole("textbox", { name: "PAN", exact: true });
    this.ifsc = this.form.getByRole("textbox", { name: "IFSC of the supplier's bank" });
    this.state = this.form.getByRole("combobox", { name: "State of the registered office" });
    this.category = this.form.getByRole("combobox", { name: "Category" });
    this.openingBalance = this.form.getByRole("textbox", { name: "Opening balance" });
    // While a calendar is open the rest of the page is hidden from assistive technology, so the date boxes and their
    // triggers are found by test id and by the end of the field id or by label attribute rather than by role.
    const formElement = page.getByTestId("supplier-example-form");
    this.agreementStart = formElement.locator("input[id$='-agreementStart']");
    this.agreementStartTrigger = formElement.locator("button[aria-label='Choose date']");
    this.validityFrom = formElement.locator("input[id$='-validity-from']");
    this.validityTo = formElement.locator("input[id$='-validity-to']");
    this.validityTrigger = formElement.locator("button[aria-label='Choose dates']");
    this.document = this.form.getByTestId("agreement-document");
    this.dropZone = this.document.getByTestId("file-drop-zone");
    this.chooseFile = this.document.getByTestId("file-drop-zone-choose");
    this.documentStatus = this.document.getByTestId("agreement-upload-status");
    this.documentAnnouncement = this.documentStatus.locator("[aria-live=polite]");
    this.documentError = this.document.getByTestId("agreement-upload-error");
    this.removeDocument = this.document.getByRole("button", { name: "Remove the document" });
    this.serverAnswer = this.form.getByRole("combobox", { name: "Server answer" });
    this.save = this.form.getByTestId("supplier-example-save");
    this.outcome = this.form.getByTestId("supplier-example-outcome");
    this.idempotencyKey = this.form.getByTestId("supplier-example-idempotency-key");
  }

  async goto(): Promise<void> {
    await this.page.goto(formKitPath);
    await expect(this.heading).toBeVisible();
    // Save is disabled in the server-rendered page and enabled once React has hydrated it, so it marks the form as live.
    await expect(this.save).toBeEnabled();
  }

  calendar(name: "Choose date" | "Choose dates"): Locator {
    return this.page.getByRole("dialog", { name, exact: true });
  }

  listbox(name: string): Locator {
    return this.page.getByRole("listbox", { name, exact: true });
  }

  summaryLink(message: string): Locator {
    return this.summary.getByRole("link", { name: message, exact: true });
  }

  fieldOf(control: Locator): Locator {
    return control.locator("xpath=ancestor::*[@data-slot='field'][1]");
  }

  async choose(select: Locator, option: string): Promise<void> {
    await select.click();
    await this.page.getByRole("option", { name: option, exact: true }).click();
    await expect(select).toHaveText(option);
    // The list returns focus to its trigger once it has finished closing; moving on before then would lose the next field's
    // focus to the trigger.
    await expect(this.page.getByRole("listbox")).toHaveCount(0);
    await expect(select).toBeFocused();
  }

  async answerWith(answer: ServerAnswer): Promise<void> {
    await this.choose(this.serverAnswer, answer);
  }

  async chooseCategory(search: string, category: string): Promise<void> {
    await this.category.fill(search);
    const list = this.listbox("Category");
    await expect(list.getByRole("option", { name: category })).toBeVisible();
    await this.category.press("ArrowDown");
    await expect(list.getByRole("option", { name: category })).toHaveAttribute("aria-selected", "true");
    await this.category.press("Enter");
    await expect(list).toBeHidden();
    await expect(this.category).toHaveValue(category);
  }

  async fill(details: SupplierDetails): Promise<void> {
    await this.legalName.fill(details.legalName);
    await this.gstin.fill(details.gstin);
    await this.pan.fill(details.pan);
    await this.ifsc.fill(details.ifsc);
    await this.choose(this.state, details.state);
    await this.chooseCategory(details.categorySearch, details.category);
    await this.openingBalance.fill(details.openingBalance);
    await this.agreementStart.fill(details.agreementStart);
    await this.validityFrom.fill(details.validityFrom);
    await this.validityTo.fill(details.validityTo);
  }

  async idempotencyKeySuffix(): Promise<string> {
    const text = (await this.idempotencyKey.textContent()) ?? "";
    const suffix = /ends in ([0-9a-f]{8})\.$/.exec(text)?.[1];
    expect(suffix, `idempotency key in "${text}"`).toBeDefined();
    return suffix ?? "";
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
