import { expect, type Locator, type Page } from "@playwright/test";

export const referencePickersPath = "/design/reference-pickers";

export type PickerName = "Country" | "State or union territory" | "Currency" | "Financial year" | "Period";

export type ChosenValue =
  "country" | "document-date" | "indian-state" | "currency" | "financial-year" | "period";

const maxArrowPresses = 300;

export class ReferencePickersPage {
  readonly heading: Locator;
  readonly form: Locator;
  readonly card: Locator;
  readonly documentDate: Locator;
  readonly values: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { name: "Reference pickers", level: 1 });
    this.form = page.getByRole("form", { name: "Choose from the reference lists (example)" });
    this.card = page
      .locator("[data-slot='card']")
      .filter({ has: page.getByTestId("reference-pickers-form") });
    this.documentDate = this.form.getByRole("textbox", { name: "Document date", exact: true });
    this.values = page.getByTestId("reference-pickers-values");
  }

  async goto(): Promise<void> {
    await this.page.goto(referencePickersPath);
    await expect(this.heading).toBeVisible();
    await this.waitUntilInteractive();
  }

  // The pickers work only through React, so the form marks itself as rendering the server's state until the page hydrates.
  async waitUntilInteractive(): Promise<void> {
    await expect(this.page.getByTestId("reference-pickers-form")).not.toHaveAttribute("data-hydrating");
  }

  picker(name: PickerName): Locator {
    return this.form.getByRole("combobox", { name, exact: true });
  }

  listbox(name: PickerName): Locator {
    return this.page.getByRole("listbox", { name, exact: true });
  }

  option(name: PickerName, label: string): Locator {
    return this.listbox(name)
      .getByRole("option")
      .filter({ has: this.page.getByText(label, { exact: true }) });
  }

  fieldOf(control: Locator): Locator {
    return control.locator("xpath=ancestor::*[@data-slot='field'][1]");
  }

  statusOf(name: PickerName): Locator {
    return this.fieldOf(this.picker(name)).getByRole("status");
  }

  chosen(value: ChosenValue): Locator {
    return this.page.getByTestId(`chosen-${value}`);
  }

  // A person types part of a name or a code, moves to the option with the arrow keys and chooses it with Enter.
  async chooseByKeyboard(name: PickerName, typed: string, label: string): Promise<void> {
    const picker = this.picker(name);
    await picker.fill("");
    await picker.pressSequentially(typed);
    const option = this.option(name, label);
    await expect(option).toBeVisible();
    for (let presses = 0; presses < maxArrowPresses; presses += 1) {
      await picker.press("ArrowDown");
      if ((await option.getAttribute("aria-selected")) === "true") break;
    }
    await expect(option).toHaveAttribute("aria-selected", "true");
    await picker.press("Enter");
    await expect(this.listbox(name)).toBeHidden();
    await expect(picker).toHaveValue(label);
  }

  async optionLabels(name: PickerName): Promise<string[]> {
    return this.listbox(name)
      .getByRole("option")
      .evaluateAll((options) =>
        options.map((option) => option.querySelector("span > span")?.textContent?.trim() ?? ""),
      );
  }
}
