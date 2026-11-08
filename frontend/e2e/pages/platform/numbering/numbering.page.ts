import { expect, type Locator, type Page, type Request } from "@playwright/test";

import type { NumberFormat, YearStyle } from "../../../fixtures/records/numbering-records";

export const numberingPath = "/platform/numbering";

const characterNames = new Map([
  ["-", "hyphen"],
  ["/", "slash"],
  [".", "full stop"],
  ["_", "underscore"],
]);

// A Server Function call is a POST to the page that renders the form.
export function isNumberingSave(request: Request): boolean {
  return (
    request.method() === "POST" &&
    new URL(request.url()).pathname === numberingPath &&
    request.headers()["next-action"] !== undefined
  );
}

// The page's own reading of a number, written apart from the product's: the prefix, the year and its separator when the year is
// shown, then the running number with zeros in front up to the digits.
export function numberText(format: NumberFormat, financialYear: string, running: bigint): string {
  const digits = running.toString().padStart(format.digits, "0");
  return format.year === "none"
    ? `${format.prefix}${digits}`
    : `${format.prefix}${yearText(financialYear, format.year)}${format.separator}${digits}`;
}

export function yearText(financialYear: string, style: Exclude<YearStyle, "none">): string {
  return style === "full" ? financialYear : financialYear.slice(2);
}

export function lastRunningNumber(format: NumberFormat, financialYear: string, maxLength: number): bigint {
  const fixed =
    format.prefix.length +
    (format.year === "none" ? 0 : yearText(financialYear, format.year).length + format.separator.length);
  return 10n ** BigInt(Math.min(maxLength - fixed, 18)) - 1n;
}

export function followingYear(financialYear: string): string {
  const start = Number(financialYear.slice(0, 4)) + 1;
  return `${start}-${String((start + 1) % 100).padStart(2, "0")}`;
}

export function characterName(character: string): string {
  const name = characterNames.get(character);
  return name === undefined ? `"${character}"` : `${character} (${name})`;
}

export function listOf(items: readonly string[], conjunction: "and" | "or"): string {
  return items.length < 2
    ? (items[0] ?? "")
    : `${items.slice(0, -1).join(", ")} ${conjunction} ${items.at(-1) ?? ""}`;
}

export function allowedCharacters(specialCharacters: string): string {
  return listOf(["capital letters", "digits", ...specialCharacters.split("").map(characterName)], "and");
}

export function eitherCharacter(specialCharacters: string): string {
  return listOf(specialCharacters.split("").map(characterName), "or");
}

export class NumberingPage {
  readonly heading: Locator;
  readonly description: Locator;
  readonly card: Locator;
  readonly listHeading: Locator;
  readonly list: Locator;
  readonly unavailable: Locator;
  readonly dialog: Locator;
  readonly form: Locator;
  readonly summary: Locator;
  readonly prefix: Locator;
  readonly yearChoices: Locator;
  readonly separatorChoices: Locator;
  readonly digits: Locator;
  readonly nextNumber: Locator;
  readonly preview: Locator;
  readonly previewStatus: Locator;
  readonly save: Locator;
  readonly cancel: Locator;
  readonly close: Locator;
  readonly reload: Locator;

  constructor(private readonly page: Page) {
    const main = page.getByRole("main");
    this.heading = main.getByRole("heading", { name: "Document numbering", level: 1 });
    this.description = main.locator("header").getByRole("paragraph").last();
    this.card = page.getByTestId("numbered-documents-card");
    this.listHeading = this.card.getByRole("heading", { name: "Kinds of document", level: 2 });
    this.list = this.card.getByRole("list", { name: "Kinds of document" });
    this.unavailable = page.getByTestId("numbering-unavailable");
    this.dialog = page.getByTestId("numbering-dialog");
    this.form = page.getByTestId("numbering-form");
    this.summary = this.dialog.locator("[data-slot='alert']");
    this.prefix = this.dialog.getByRole("textbox", { name: "Starts with" });
    this.yearChoices = this.dialog.getByRole("radiogroup", { name: "Financial year" });
    this.separatorChoices = this.dialog.getByRole("radiogroup", { name: "After the year" });
    this.digits = this.dialog.getByRole("textbox", { name: "Digits" });
    this.nextNumber = this.dialog.getByRole("textbox", { name: "Next number" });
    this.preview = page.getByTestId("numbering-preview");
    this.previewStatus = page.getByTestId("numbering-preview-status");
    this.save = page.getByTestId("numbering-save");
    this.cancel = this.dialog.getByRole("button", { name: "Cancel", exact: true });
    this.close = this.dialog.getByRole("button", { name: "Close", exact: true });
    this.reload = page.getByTestId("numbering-reload");
  }

  async goto(): Promise<void> {
    await this.page.goto(numberingPath);
    await expect(this.page.getByRole("status", { name: /^Loading/ })).toHaveCount(0);
    await expect(this.heading).toBeVisible();
  }

  // Each kind's button opens a dialog, so it is disabled in the server's HTML and marks itself as rendering that state until
  // React hydrates it.
  async waitUntilInteractive(): Promise<void> {
    await expect(this.page.getByRole("main").locator("[data-hydrating]")).toHaveCount(0);
  }

  document(title: string): Locator {
    return this.page
      .getByTestId("numbered-document")
      .filter({ has: this.page.getByRole("heading", { name: title, exact: true, level: 3 }) });
  }

  action(title: string): Locator {
    return this.document(title).getByTestId("numbering-action");
  }

  years(title: string): Locator {
    return this.document(title).getByTestId("numbered-document-years");
  }

  status(title: string): Locator {
    return this.document(title).getByTestId("numbered-document-status");
  }

  pending(title: string): Locator {
    return this.document(title).getByTestId("numbered-document-pending");
  }

  yearChoice(label: string): Locator {
    return this.yearChoices.getByRole("radio", { name: label, exact: true });
  }

  separatorChoice(label: string): Locator {
    return this.separatorChoices.getByRole("radio", { name: label, exact: true });
  }

  fieldOf(control: Locator): Locator {
    return control.locator("xpath=ancestor::*[@data-slot='field'][1]");
  }

  choiceFieldOf(group: Locator): Locator {
    return group.locator("xpath=ancestor::fieldset[1]");
  }

  // A dialog that starts to close turns its data-state to "closed" at once but stays on the page through its exit animation,
  // so whether it closed is judged by its count once it has gone.
  async expectDialogClosed(): Promise<void> {
    await expect(this.dialog).toHaveCount(0);
  }
}
