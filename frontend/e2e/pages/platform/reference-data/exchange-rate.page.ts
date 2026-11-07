import { expect, type Locator, type Page, type Request } from "@playwright/test";

import { exchangeRatePath, newExchangeRatePath } from "./exchange-rates.page";

// A Server Function call is a POST to the page that renders the form.
export function isServerFunctionCall(request: Request, pathname: string): boolean {
  return (
    request.method() === "POST" &&
    new URL(request.url()).pathname === pathname &&
    request.headers()["next-action"] !== undefined
  );
}

export function isRecordingRequest(request: Request): boolean {
  return isServerFunctionCall(request, newExchangeRatePath);
}

export class NewExchangeRatePage {
  readonly heading: Locator;
  readonly form: Locator;
  readonly summary: Locator;
  readonly currency: Locator;
  readonly day: Locator;
  readonly dayTrigger: Locator;
  readonly dayCalendar: Locator;
  readonly rate: Locator;
  readonly unit: Locator;
  readonly source: Locator;
  readonly save: Locator;
  readonly cancel: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("main").getByRole("heading", { name: "Record a rate", level: 1 });
    this.form = page.getByRole("form", { name: "The rate" });
    this.summary = this.form.locator("[data-slot='alert']");
    this.currency = this.form.getByRole("combobox", { name: "Currency" });
    // While the calendar is open the rest of the page is hidden from assistive technology, so the day's box and its trigger
    // are found by the end of the field id and by label attribute rather than by role.
    const form = page.getByTestId("exchange-rate-form");
    this.day = form.locator("input[id$='-rateDate']");
    this.dayTrigger = form.locator("button[aria-label='Choose date']");
    this.dayCalendar = page.getByRole("dialog", { name: "Choose date", exact: true });
    this.rate = this.form.getByRole("textbox", { name: "Rate" });
    this.unit = this.form.getByRole("combobox", { name: "Units" });
    this.source = this.form.getByRole("textbox", { name: "Source" });
    this.save = page.getByTestId("exchange-rate-save");
    this.cancel = this.form.getByRole("link", { name: "Cancel", exact: true });
  }

  async goto(): Promise<void> {
    await this.page.goto(newExchangeRatePath);
    await expect(this.heading).toBeVisible();
    await expect(this.save).toBeEnabled();
  }

  fieldOf(control: Locator): Locator {
    return control.locator("xpath=ancestor::*[@data-slot='field'][1]");
  }

  // The search text is a currency's code, which only that currency's label holds, so the list it leaves holds the one option
  // to choose, which the keyboard then makes active and chooses.
  async chooseCurrency(code: string, option: string): Promise<void> {
    await this.currency.fill(code);
    const list = this.page.getByRole("listbox");
    await expect(list.getByRole("option")).toHaveCount(1);
    await expect(list.getByRole("option", { name: option })).toBeVisible();
    await this.currency.press("ArrowDown");
    await expect(list.getByRole("option", { name: option })).toHaveAttribute("aria-selected", "true");
    await this.currency.press("Enter");
    await expect(list).toBeHidden();
    await expect(this.currency).toHaveValue(option);
  }

  async chooseUnit(unit: string): Promise<void> {
    await chooseUnitOf(this.page, this.unit, unit);
  }

  async fill(rate: {
    code: string;
    option: string;
    day: string;
    rate: string;
    source?: string;
  }): Promise<void> {
    await this.chooseCurrency(rate.code, rate.option);
    await this.day.fill(rate.day);
    await this.rate.fill(rate.rate);
    if (rate.source !== undefined) await this.source.fill(rate.source);
  }
}

export class ExchangeRatePage {
  readonly heading: Locator;
  readonly header: Locator;
  readonly details: Locator;
  readonly actions: Locator;
  readonly correct: Locator;
  readonly remove: Locator;
  readonly removeError: Locator;
  readonly correctDialog: Locator;
  readonly correctSummary: Locator;
  readonly correctRate: Locator;
  readonly correctUnit: Locator;
  readonly correctSource: Locator;
  readonly correctSave: Locator;
  readonly correctCancel: Locator;
  readonly correctClose: Locator;
  readonly showLatest: Locator;
  readonly history: Locator;
  readonly historyOrItsLoading: Locator;
  readonly historyLink: Locator;
  readonly unavailable: Locator;

  constructor(private readonly page: Page) {
    const main = page.getByRole("main");
    this.heading = main.getByRole("heading", { level: 1 });
    this.header = main.locator("header").first();
    this.details = page.getByTestId("exchange-rate-details");
    this.actions = page.getByTestId("exchange-rate-actions");
    this.correct = page.getByTestId("exchange-rate-correct");
    this.remove = page.getByTestId("exchange-rate-remove");
    this.removeError = page.getByTestId("exchange-rate-remove-error");
    this.correctDialog = page.getByRole("dialog", { name: /^Correct the rate of / });
    this.correctSummary = this.correctDialog.locator("[data-slot='alert']");
    this.correctRate = this.correctDialog.getByRole("textbox", { name: "Rate" });
    this.correctUnit = this.correctDialog.getByRole("combobox", { name: "Units" });
    this.correctSource = this.correctDialog.getByRole("textbox", { name: "Source" });
    this.correctSave = this.correctDialog.getByTestId("exchange-rate-correct-save");
    this.correctCancel = this.correctDialog.getByRole("button", { name: "Cancel", exact: true });
    this.correctClose = this.correctDialog.getByRole("button", { name: "Close", exact: true });
    this.showLatest = this.correctDialog.getByTestId("exchange-rate-reload");
    this.history = page.getByTestId("record-history");
    // The history streams in after the rest of the page, so a page that shows it holds either the history or its loading
    // state from the moment its heading shows.
    this.historyOrItsLoading = page
      .getByTestId("record-history")
      .or(page.getByTestId("record-history-loading"));
    this.historyLink = page.getByTestId("record-history-link");
    this.unavailable = page.getByTestId("exchange-rate-unavailable");
  }

  async goto(id: string): Promise<void> {
    await this.page.goto(exchangeRatePath(id));
    await expect(this.heading).toBeVisible();
  }

  // Every button of the page that opens a dialog is disabled in the server's HTML and marks itself as rendering that state
  // until React hydrates it, when its dialog can open.
  async waitUntilInteractive(): Promise<void> {
    await expect(this.page.getByRole("main").locator("[data-hydrating]")).toHaveCount(0);
  }

  confirmation(title: string): Locator {
    return this.page.getByRole("alertdialog", { name: title });
  }

  fieldOf(control: Locator): Locator {
    return control.locator("xpath=ancestor::*[@data-slot='field'][1]");
  }

  async chooseUnit(unit: string): Promise<void> {
    await chooseUnitOf(this.page, this.correctUnit, unit);
  }
}

// On mobile Safari a Radix select returns focus to its trigger only after its closing animation, so the next field is used
// once the list has closed and the trigger holds focus again.
async function chooseUnitOf(page: Page, trigger: Locator, unit: string): Promise<void> {
  await trigger.click();
  const option = page.getByRole("option", { name: unit, exact: true });
  await option.click();
  await expect(page.getByRole("listbox")).toBeHidden();
  await expect(trigger).toHaveText(unit);
  await expect(trigger).toBeFocused();
}
