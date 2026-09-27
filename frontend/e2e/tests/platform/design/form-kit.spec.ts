import { randomUUID } from "node:crypto";

import type { Locator, Page, Request, Response } from "@playwright/test";

import { expect, forEachTheme, test } from "../../../fixtures/test";
import {
  FormKitPage,
  formKitPath,
  isSaveRequest,
  validSupplier,
  type DroppedFile,
} from "../../../pages/platform/design/form-kit.page";

const attachmentsApi = "/api/platform/attachments";

const exampleReference = "4bf92f3577b34da6a3ce929d0e0e4736";

const savedMessage = "Saved as an example. Nothing was stored.";

const pngSignature = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

const filler = Buffer.from(Array.from({ length: 251 }, (_, index) => index));

// The bytes match the attachments spec's 256-byte PNG and only the name is unique, so every run deduplicates to one stored file.
function png(): DroppedFile {
  return {
    name: `e2e-${randomUUID().slice(-12)}.png`,
    mimeType: "image/png",
    buffer: Buffer.concat([pngSignature, Buffer.alloc(256 - pngSignature.length, filler)]),
  };
}

function isUploadRequest(request: Request): boolean {
  return request.method() === "POST" && new URL(request.url()).pathname === attachmentsApi;
}

function isUpload(response: Response): boolean {
  return isUploadRequest(response.request());
}

function watch(page: Page, matches: (request: Request) => boolean): { count: () => number } {
  let seen = 0;
  page.on("request", (request) => {
    if (matches(request)) seen += 1;
  });
  return { count: () => seen };
}

async function hold(
  page: Page,
  pattern: string,
  matches: (request: Request) => boolean,
): Promise<() => void> {
  let release: () => void = () => undefined;
  const released = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route(pattern, async (route) => {
    if (!matches(route.request())) {
      await route.fallback();
      return;
    }
    await released;
    await route.continue();
  });
  return release;
}

async function expectFieldError(field: Locator, message: RegExp): Promise<void> {
  await expect(field).toHaveAttribute("aria-invalid", "true");
  await expect(field).toHaveAccessibleDescription(message);
}

async function hasHorizontalOverflow(page: Page): Promise<boolean> {
  return page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
}

test.describe("form kit", () => {
  test.beforeEach(({ browserName }) => {
    test.slow(
      browserName === "webkit",
      "WebKit takes about twice as long per action, and each test fills a whole form",
    );
  });

  test.describe("before the page is interactive", () => {
    test.use({ javaScriptEnabled: false });

    test("keeps Save disabled so the form cannot be sent", async ({ page }) => {
      const formKit = new FormKitPage(page);
      const saves = watch(page, isSaveRequest);
      await page.goto(formKitPath);

      await expect(formKit.heading).toBeVisible();
      await expect(formKit.save).toBeDisabled();
      await formKit.legalName.fill(validSupplier.legalName);
      await formKit.legalName.press("Enter");
      await expect(formKit.legalName).toHaveValue(validSupplier.legalName);
      await expect(page).toHaveURL(new RegExp(`${formKitPath}$`));
      await expect(formKit.save).toBeDisabled();
      expect(saves.count(), "Server Function calls").toBe(0);
    });
  });

  forEachTheme(
    "focuses the first invalid field and explains every client error",
    async ({ page, capture }) => {
      const formKit = new FormKitPage(page);
      const saves = watch(page, isSaveRequest);
      await formKit.goto();

      await expect(page).toHaveTitle("Form kit · ERP-AI-Pro");
      await expect(formKit.form).toMatchAriaSnapshot(`
      - form "Register a supplier (example)":
        - group:
          - text: Legal name
          - textbox "Legal name"
          - paragraph: As printed on the supplier's PAN card.
        - group:
          - text: GSTIN
          - textbox "GSTIN"
          - paragraph: 15 characters. Spaces and dashes are removed as you type.
        - group:
          - text: PAN
          - textbox "PAN"
          - paragraph: 10 characters.
        - group:
          - text: IFSC of the supplier's bank
          - textbox "IFSC of the supplier's bank"
          - paragraph: 11 characters.
        - group:
          - text: State of the registered office
          - combobox "State of the registered office": Choose a state
        - group:
          - text: Category
          - group:
            - combobox "Category"
            - group:
              - button "Show options"
            - status
          - paragraph: Type to narrow the list.
        - group:
          - text: Opening balance
          - group:
            - group: ₹
            - textbox "Opening balance":
              - /placeholder: "0.00"
          - paragraph: Optional. What the company owes the supplier today, up to ₹10,00,00,000.00.
        - group:
          - text: Agreement starts on
          - group:
            - textbox "Agreement starts on":
              - /placeholder: dd-mm-yyyy
            - group:
              - button "Choose date"
          - paragraph: Between 01-04-2020 and 31-03-2035.
        - group:
          - text: Validity From
          - group:
            - textbox "Validity From":
              - /placeholder: dd-mm-yyyy
          - text: To
          - group:
            - textbox "Validity To":
              - /placeholder: dd-mm-yyyy
          - button "Choose dates"
          - paragraph: The first and the last day the agreement applies.
        - group "Agreement document":
          - text: Agreement document
          - paragraph: Optional. The file is stored encrypted in the attachments list; the supplier itself is never saved.
          - group "Drop the signed agreement here or choose it":
            - paragraph: Drop the signed agreement here or choose it
            - paragraph: PDF or PNG, up to 10 MB.
            - button "Choose a file"
        - group:
          - text: Server answer
          - combobox "Server answer": Accept the supplier
          - paragraph: "For this page only: how the example Server Function answers the next save. Nothing reaches the API."
        - button "Save"
        - status
        - paragraph: No idempotency key sent yet.
    `);
      await capture("form-empty", formKit.form);

      await formKit.save.click();
      await expect(formKit.legalName).toBeFocused();
      await expectFieldError(
        formKit.legalName,
        /^As printed on the supplier's PAN card\. Enter the supplier's legal name\.$/,
      );
      await expectFieldError(formKit.gstin, /Enter the GSTIN\.$/);
      await expectFieldError(formKit.pan, /Enter the PAN\.$/);
      await expectFieldError(formKit.ifsc, /Enter the IFSC\.$/);
      await expectFieldError(formKit.state, /^Choose the state of the registered office\.$/);
      await expectFieldError(formKit.category, /Choose a category from the list\.$/);
      await expectFieldError(formKit.agreementStart, /Enter the date the agreement starts\.$/);
      await expectFieldError(formKit.validityFrom, /Enter the start date\./);
      await expectFieldError(formKit.validityTo, /Enter the end date\./);
      await expect(formKit.openingBalance).not.toHaveAttribute("aria-invalid");
      await expect(formKit.summary).toHaveCount(0);
      await capture("client-errors", formKit.form);
      expect(await hasHorizontalOverflow(page), "horizontal overflow").toBe(false);

      await formKit.legalName.fill(validSupplier.legalName);
      await expect(formKit.legalName).not.toHaveAttribute("aria-invalid");
      await formKit.save.click();
      await expect(formKit.gstin).toBeFocused();
      await page.keyboard.type("27 aaacd-1234");
      await expect(formKit.gstin).toHaveValue("27AAACD1234");
      await expectFieldError(
        formKit.gstin,
        /Enter a 15-character GSTIN: two digits, then 13 letters or digits\.$/,
      );
      await formKit.openingBalance.fill("100000000.01");
      await formKit.openingBalance.blur();
      await expect(formKit.openingBalance).toHaveValue("10,00,00,000.01");
      await expectFieldError(formKit.openingBalance, /Enter an amount of at most ₹10,00,00,000\.00\.$/);
      await formKit.agreementStart.fill("31-02-2026");
      await formKit.agreementStart.blur();
      await expectFieldError(
        formKit.agreementStart,
        /Enter a real date as day-month-year, for example 31-03-2026\.$/,
      );
      await formKit.validityFrom.fill("01-04-2027");
      await formKit.validityTo.fill("31-03-2027");
      await formKit.validityTo.blur();
      await expectFieldError(formKit.validityTo, /The end date must be on or after the start date\.$/);
      expect(saves.count(), "Server Function calls").toBe(0);
    },
  );

  forEachTheme(
    "puts a server field error on its field and in the focused summary",
    async ({ page, capture }) => {
      const formKit = new FormKitPage(page);
      await formKit.goto();
      await formKit.fill(validSupplier);
      await expect(formKit.gstin).toHaveValue("29AABCG1234K1Z5");
      await expect(formKit.pan).toHaveValue("AABCG1234K");
      await expect(formKit.ifsc).toHaveValue("HDFC0001234");

      await formKit.answerWith("Refuse the legal name");
      await formKit.save.click();

      await expect(formKit.summary).toBeFocused();
      await expect(formKit.summary).toMatchAriaSnapshot(`
      - alert:
        - text: Some details need attention.
        - list:
          - listitem:
            - link "A supplier with this legal name is already registered.":
              - /url: /^#.+-legalName$/
    `);
      await expectFieldError(formKit.legalName, /A supplier with this legal name is already registered\.$/);
      await capture("server-field-error", formKit.form);

      await formKit.summaryLink("A supplier with this legal name is already registered.").click();
      await expect(formKit.legalName).toBeFocused();
      await formKit.legalName.fill("Globex Cloud Services (India) Private Limited");
      await expect(formKit.legalName).not.toHaveAttribute("aria-invalid");
      await expect(formKit.summary).toHaveCount(0);
    },
  );

  forEachTheme(
    "maps a validation problem with nested keys onto the form's fields",
    async ({ page, capture }) => {
      const formKit = new FormKitPage(page);
      await formKit.goto();
      await formKit.fill(validSupplier);

      await formKit.answerWith("Refuse several nested details");
      await formKit.save.click();

      await expect(formKit.summary).toBeFocused();
      await expect(formKit.summary).toMatchAriaSnapshot(`
      - alert:
        - text: Some details need attention.
        - list:
          - listitem: Check these details against the supplier's registration certificate.
          - listitem:
            - link "This GSTIN is registered to a different PAN.":
              - /url: /^#.+-gstin$/
          - listitem:
            - link "No bank branch uses this IFSC.":
              - /url: /^#.+-ifsc$/
          - listitem:
            - link "The opening balance is above the credit limit agreed with the supplier.":
              - /url: /^#.+-openingBalance$/
          - listitem:
            - link "The agreement must run until at least the end of the financial year.":
              - /url: /^#.+-validity-to$/
    `);
      await expectFieldError(formKit.gstin, /This GSTIN is registered to a different PAN\.$/);
      await expectFieldError(formKit.ifsc, /No bank branch uses this IFSC\.$/);
      await expectFieldError(
        formKit.openingBalance,
        /The opening balance is above the credit limit agreed with the supplier\.$/,
      );
      await expectFieldError(
        formKit.validityTo,
        /The agreement must run until at least the end of the financial year\.$/,
      );
      await expect(formKit.legalName).not.toHaveAttribute("aria-invalid");
      await capture("nested-problem", formKit.form);
      expect(await hasHorizontalOverflow(page), "horizontal overflow").toBe(false);

      await formKit.summaryLink("No bank branch uses this IFSC.").click();
      await expect(formKit.ifsc).toBeFocused();
      await formKit
        .summaryLink("The agreement must run until at least the end of the financial year.")
        .click();
      await expect(formKit.validityTo).toBeFocused();

      await formKit.ifsc.fill("ICIC0000001");
      await expect(formKit.ifsc).not.toHaveAttribute("aria-invalid");
      await expect(formKit.summaryLink("No bank branch uses this IFSC.")).toHaveCount(0);
      await expect(formKit.summaryLink("This GSTIN is registered to a different PAN.")).toBeVisible();
    },
  );

  forEachTheme(
    "keeps the idempotency key after an undecided answer and replaces it after a decided one",
    async ({ page, capture }) => {
      const formKit = new FormKitPage(page);
      await formKit.goto();
      await formKit.fill(validSupplier);

      await formKit.answerWith("Fail with a server error");
      await formKit.save.click();
      await expect(formKit.summary).toBeFocused();
      await expect(formKit.summary).toMatchAriaSnapshot(`
        - alert:
          - text: The server could not finish this. Try again.
          - paragraph: "Reference: ${exampleReference}"
      `);
      await capture("server-error", formKit.summary);
      expect(await hasHorizontalOverflow(page), "horizontal overflow").toBe(false);
      const undecided = await formKit.idempotencyKeySuffix();

      await formKit.answerWith("Do not respond (API unreachable)");
      await formKit.save.click();
      await expect(formKit.summary).toHaveText("The ERP service did not respond. Try again.");
      await expect(formKit.summary).toBeFocused();
      expect(await formKit.idempotencyKeySuffix(), "key after an unreachable API").toBe(undecided);

      await formKit.answerWith("Report the supplier as already registered");
      await formKit.save.click();
      await expect(formKit.summary).toContainText(
        "A supplier with this GSTIN is already registered. Open that supplier instead of adding it again.",
      );
      await expect(formKit.summary).toContainText(`Reference: ${exampleReference}`);
      expect(await formKit.idempotencyKeySuffix(), "key sent again after two undecided answers").toBe(
        undecided,
      );

      await formKit.answerWith("Accept the supplier");
      await formKit.save.click();
      await expect(formKit.outcome).toHaveText(savedMessage);
      await expect(formKit.summary).toHaveCount(0);
      const afterConflict = await formKit.idempotencyKeySuffix();
      expect(afterConflict, "key after a conflict").not.toBe(undecided);

      await formKit.save.click();
      await expect
        .poll(() => formKit.idempotencyKeySuffix(), { message: "key after a success" })
        .not.toBe(afterConflict);
      await expect(formKit.outcome).toHaveText(savedMessage);
    },
  );

  forEachTheme("shows the pending save and then the success", async ({ page, capture }) => {
    const formKit = new FormKitPage(page);
    const saves = watch(page, isSaveRequest);
    await formKit.goto();
    await formKit.fill(validSupplier);

    const release = await hold(page, `**${formKitPath}`, isSaveRequest);
    const sent = page.waitForRequest(isSaveRequest);
    await formKit.save.click();
    const request = await sent;

    await expect(formKit.save).toHaveAttribute("aria-busy", "true");
    await expect(formKit.save).toBeDisabled();
    await expect(formKit.save).toHaveAccessibleName("Save");
    await formKit.legalName.press("Enter");
    await capture("pending", formKit.form);
    const body = request.postData() ?? "";
    expect(body, "the values sent").toContain(validSupplier.legalName);
    expect(body, "the idempotency key sent").toMatch(
      new RegExp(`"[0-9a-f-]{28}${await formKit.idempotencyKeySuffix()}"`),
    );
    release();

    await expect(formKit.outcome).toHaveText(savedMessage);
    await expect(formKit.save).not.toHaveAttribute("aria-busy");
    await expect(formKit.save).toBeEnabled();
    expect(saves.count(), "Server Function calls").toBe(1);
  });

  forEachTheme(
    "attaches the agreement dropped on the drop zone and sends its id with the form",
    async ({ page, capture }) => {
      const formKit = new FormKitPage(page);
      const uploads = watch(page, isUploadRequest);
      const file = png();
      await formKit.goto();
      await formKit.fill(validSupplier);

      const chooser = page.waitForEvent("filechooser");
      await formKit.chooseFile.click();
      await (
        await chooser
      ).setFiles({ name: "notes.txt", mimeType: "text/plain", buffer: Buffer.from("GST") });
      await expect(formKit.documentError).toHaveText(
        "notes.txt: only PDF files and PNG images can be attached here.",
      );
      expect(uploads.count(), "uploads of a refused file").toBe(0);

      const dataTransfer = await formKit.transferOf(file);
      await expect(async () => {
        await formKit.dragOver(dataTransfer);
        await expect(formKit.dropZone).toHaveAttribute("data-dragging", "true", { timeout: 1_000 });
      }).toPass();

      // WebKit sometimes forwards an intercepted multipart request with an empty file part, which the API rightly refuses,
      // so only the other engines hold the upload to check the uploading state; WebKit uploads straight through.
      const holdsUpload = page.context().browser()?.browserType().name() !== "webkit";
      const release = holdsUpload
        ? await hold(page, `**${attachmentsApi}`, isUploadRequest)
        : () => undefined;
      const uploaded = page.waitForResponse(isUpload);
      await formKit.drop(dataTransfer);
      if (holdsUpload) {
        await expect(formKit.documentStatus).toHaveAttribute("data-state", "uploading");
        await expect(formKit.documentAnnouncement).toHaveText(`Uploading ${file.name}…`);
        await expect(formKit.save).toBeDisabled();
        await capture("document-uploading", formKit.document);
      }
      release();

      const response = await uploaded;
      expect(response.status()).toBe(201);
      const { id } = (await response.json()) as { id?: unknown };
      expect(typeof id, "attachment id").toBe("string");
      try {
        await expect(formKit.documentAnnouncement).toHaveText(`${file.name} is attached.`);
        await expect(formKit.dropZone).toHaveCount(0);
        await expect(formKit.save).toBeEnabled();
        await capture("document-attached", formKit.document);

        const sent = page.waitForRequest(isSaveRequest);
        await formKit.save.click();
        expect((await sent).postData() ?? "", "the attachment id sent with the form").toContain(String(id));
        await expect(formKit.outcome).toHaveText(savedMessage);

        await formKit.removeDocument.click();
        await expect(formKit.dropZone).toBeVisible();
        await expect(formKit.documentAnnouncement).toHaveText("");
      } finally {
        const deleted = await page.request.delete(`${attachmentsApi}/${String(id)}`);
        expect(deleted.status(), "attachment deleted").toBe(204);
      }
    },
  );

  forEachTheme("chooses the category and the dates with the keyboard", async ({ page, capture }) => {
    const formKit = new FormKitPage(page);
    await formKit.goto();

    const categories = formKit.listbox("Category");
    await formKit.category.focus();
    await page.keyboard.press("Alt+ArrowDown");
    await expect(categories).toBeVisible();
    await expect(formKit.category).toHaveAttribute("aria-expanded", "true");
    await expect(formKit.category).not.toHaveAttribute("aria-activedescendant");
    await expect(categories.getByRole("option", { name: /^Imports/ })).toHaveAttribute(
      "aria-disabled",
      "true",
    );
    await page.keyboard.press("Escape");
    await expect(categories).toBeHidden();

    await page.keyboard.type("cafe");
    await expect(categories.getByRole("option")).toHaveText(["Café and pantry"]);
    await page.keyboard.press("ArrowDown");
    await expect(categories.getByRole("option", { name: "Café and pantry" })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    await capture("category-filtered", categories);
    await page.keyboard.press("Enter");
    await expect(categories).toBeHidden();
    await expect(formKit.category).toHaveValue("Café and pantry");
    await expect(formKit.category).toBeFocused();

    await formKit.category.fill("zzz");
    await expect(formKit.form.getByRole("status").filter({ hasText: "No category matches." })).toHaveCount(1);
    await expect(formKit.category).toHaveAttribute("aria-expanded", "false");
    await page.keyboard.press("Escape");
    await expect(formKit.category).toHaveValue("zzz");
    await page.keyboard.press("Escape");
    await expect(formKit.category).toHaveValue("Café and pantry");

    await formKit.agreementStart.fill("15-06-2026");
    await formKit.agreementStartTrigger.focus();
    await page.keyboard.press("Enter");
    const calendar = formKit.calendar("Choose date");
    await expect(calendar).toBeVisible();
    await expect(formKit.agreementStartTrigger).toHaveAttribute("aria-expanded", "true");
    await expect(calendar.getByRole("button", { name: /\b15 June 2026/ })).toBeFocused();
    await capture("agreement-start-calendar", calendar);
    await page.keyboard.press("ArrowRight");
    await expect(calendar.getByRole("button", { name: /\b16 June 2026/ })).toBeFocused();
    await page.keyboard.press("Enter");
    await expect(calendar).toBeHidden();
    await expect(formKit.agreementStart).toHaveValue("16-06-2026");
    await expect(formKit.agreementStartTrigger).toBeFocused();
    await expect(formKit.agreementStart).not.toHaveAttribute("aria-invalid");

    await formKit.validityFrom.fill("01-04-2026");
    await formKit.validityTrigger.focus();
    await page.keyboard.press("Enter");
    const rangeCalendar = formKit.calendar("Choose dates");
    await expect(rangeCalendar).toBeVisible();
    await expect(formKit.validityTrigger).toHaveAttribute("aria-expanded", "true");
    await expect(rangeCalendar.getByRole("button", { name: /\b1 April 2026/ })).toBeFocused();
    await page.keyboard.press("ArrowDown");
    await page.keyboard.press("ArrowDown");
    await expect(rangeCalendar.getByRole("button", { name: /\b15 April 2026/ })).toBeFocused();
    await capture("validity-calendar", rangeCalendar);
    await page.keyboard.press("Enter");
    await expect(rangeCalendar).toBeHidden();
    await expect(formKit.validityFrom).toHaveValue("01-04-2026");
    await expect(formKit.validityTo).toHaveValue("15-04-2026");
    await expect(formKit.validityTrigger).toBeFocused();
  });

  test.describe("on the narrowest screen", () => {
    test.use({ viewport: { width: 320, height: 720 } });

    test("keeps the form and its error summary inside the screen", async ({ page }) => {
      const formKit = new FormKitPage(page);
      await formKit.goto();
      expect(await hasHorizontalOverflow(page), "horizontal overflow of the empty form").toBe(false);

      await formKit.fill(validSupplier);
      await formKit.answerWith("Refuse several nested details");
      await formKit.save.click();
      await expect(formKit.summary).toBeFocused();
      expect(await hasHorizontalOverflow(page), "horizontal overflow with field errors").toBe(false);

      await formKit.answerWith("Fail with a server error");
      await formKit.save.click();
      await expect(formKit.summary).toContainText(`Reference: ${exampleReference}`);
      expect(await hasHorizontalOverflow(page), "horizontal overflow with a reference").toBe(false);
    });
  });
});
