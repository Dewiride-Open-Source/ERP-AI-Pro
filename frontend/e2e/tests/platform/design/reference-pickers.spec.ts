import { addUnverifiedSessionCookie } from "../../../fixtures/sign-in";
import { gatedBaseURL, offlineBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { sessionRenewed } from "../../../pages/identity/auth/session.page";
import { ReferencePickersPage, type PickerName } from "../../../pages/platform/design/reference-pickers.page";
import {
  dayAfter,
  dayMonthYear,
  readCountries,
  readCurrencies,
  readFinancialYears,
  readIndianStates,
} from "../../../pages/platform/reference-data/reference-lists.page";

const notADate = "Enter a real date as day-month-year, for example 31-07-2020.";

const listPickers = ["Country", "State or union territory", "Currency", "Financial year"] as const;

test.describe("reference pickers", () => {
  forEachTheme(
    "offer each reference list, find an entry by the code typed in it and keep every chosen value",
    async ({ page, capture }) => {
      const countries = await readCountries(page.request);
      const states = await readIndianStates(page.request);
      const currencies = await readCurrencies(page.request);
      const [year] = await readFinancialYears(page.request, "2025-26", "2025-26");
      const india = countries.find((country) => country.code === "IN");
      const maharashtra = states.find((state) => state.gstCode === "27" && state.validUntil === null);
      const dollar = currencies.find((currency) => currency.code === "USD");
      const rupee = currencies.find((currency) => currency.code === "INR");
      expect(india, "the country IN").toBeDefined();
      expect(maharashtra, "the GST State code 27 in force today").toBeDefined();
      expect(dollar, "the currency USD").toBeDefined();
      expect(rupee, "the currency INR").toBeDefined();
      expect(year, "the financial year 2025-26").toBeDefined();
      const indiaLabel = `${india!.name} (IN)`;
      const april = year!.periods[0]!;
      const pickers = new ReferencePickersPage(page);
      await pickers.goto();

      await expect(page).toHaveTitle("Reference pickers · ERP-AI-Pro");
      await expect(pickers.form).toMatchAriaSnapshot(`
        - form "Choose from the reference lists (example)":
          - group:
            - text: Country
            - group:
              - combobox "Country"
              - group:
                - button "Show options Country"
              - status
            - paragraph: Type its name or its two-letter code.
          - group:
            - text: Currency
            - group:
              - combobox "Currency"
              - group:
                - button "Show options Currency"
              - status
            - paragraph: A foreign currency, so the Indian rupee is left out.
          - group:
            - text: Document date
            - group:
              - textbox "Document date":
                - /placeholder: dd-mm-yyyy
              - group:
                - button "Choose date"
            - paragraph: The State codes in force on this day are offered; today's when it is empty.
          - group:
            - text: State or union territory
            - group:
              - combobox "State or union territory"
              - group:
                - button "Show options State or union territory"
              - status
            - paragraph: Type its name or its GST State code.
          - group:
            - text: Financial year
            - group:
              - combobox "Financial year"
              - group:
                - button "Show options Financial year"
              - status
          - group:
            - text: Period
            - group:
              - combobox "Period"
              - group:
                - button "Show options Period"
              - status
            - paragraph: The months of the chosen financial year.
          - heading "Chosen values" [level=3]
          - term: Country
          - definition: None
          - term: Document date
          - definition: None
          - term: State or union territory
          - definition: None
          - term: Currency
          - definition: None
          - term: Financial year
          - definition: None
          - term: Period
          - definition: None
      `);
      await capture("reference-pickers", pickers.card);

      const country = pickers.picker("Country");
      await country.focus();
      await page.keyboard.type("IN");
      await expect(pickers.option("Country", indiaLabel)).toBeVisible();
      await expect(country).toHaveAttribute("aria-expanded", "true");
      await capture("reference-pickers-countries", pickers.listbox("Country"));
      await pickers.chooseByKeyboard("Country", "IN", indiaLabel);
      await expect(pickers.chosen("country")).toHaveText("IN");

      const stateLabel = `27 ${maharashtra!.name}`;
      await pickers.chooseByKeyboard("State or union territory", "27", stateLabel);
      await expect(pickers.chosen("indian-state")).toHaveText("27");

      const currency = pickers.picker("Currency");
      await currency.focus();
      await page.keyboard.type("INR");
      await expect(pickers.option("Currency", `INR ${rupee!.name}`)).toHaveCount(0);
      await expect(pickers.statusOf("Currency")).toHaveText("No currency matches.");
      await pickers.chooseByKeyboard("Currency", "USD", `USD ${dollar!.name}`);
      await expect(pickers.chosen("currency")).toHaveText("USD");

      await pickers.chooseByKeyboard("Financial year", "2025-26", "2025-26");
      await expect(pickers.chosen("financial-year")).toHaveText("2025-26");
      await pickers.chooseByKeyboard("Period", april.label, april.label);
      await expect(pickers.chosen("period")).toHaveText(april.start);

      await page.keyboard.press("Tab");
      await expect(pickers.picker("Period")).not.toBeFocused();
      await expect(country).toHaveValue(indiaLabel);
      await expect(pickers.picker("State or union territory")).toHaveValue(stateLabel);
      await expect(currency).toHaveValue(`USD ${dollar!.name}`);
      await expect(pickers.picker("Financial year")).toHaveValue("2025-26");
      await expect(pickers.picker("Period")).toHaveValue(april.label);
      await expect(pickers.values).toMatchAriaSnapshot(`
        - term: Country
        - definition: IN
        - term: Document date
        - definition: None
        - term: State or union territory
        - definition: "27"
        - term: Currency
        - definition: USD
        - term: Financial year
        - definition: "2025-26"
        - term: Period
        - definition: "${april.start}"
      `);
      await capture("reference-pickers-chosen", pickers.card);
    },
  );

  test("offers the State codes in force on the document's date and today's without one", async ({ page }) => {
    const states = await readIndianStates(page.request);
    const ended = states.find((state) => state.gstCode === "25" && state.validUntil !== null);
    expect(ended, "the GST State code 25, which no longer applies").toBeDefined();
    const lastDay = ended!.validUntil!;
    const label = `25 ${ended!.name}`;
    const pickers = new ReferencePickersPage(page);
    await pickers.goto();
    const state = pickers.picker("State or union territory");

    await state.focus();
    await page.keyboard.type("25");
    await expect(pickers.statusOf("State or union territory")).toHaveText(
      "No state or union territory matches.",
    );
    await expect(state).toHaveAttribute("aria-expanded", "false");
    await state.press("Escape");
    await state.press("Escape");
    await expect(state).toHaveValue("");

    await pickers.documentDate.fill(dayMonthYear(lastDay));
    await expect(pickers.chosen("document-date")).toHaveText(lastDay);
    await pickers.chooseByKeyboard("State or union territory", "25", label);
    await expect(pickers.chosen("indian-state")).toHaveText("25");

    await pickers.documentDate.fill(dayMonthYear(dayAfter(lastDay)));
    await expect(pickers.chosen("document-date")).toHaveText(dayAfter(lastDay));
    await expect(state).toHaveValue("25 (name not available)");
    await expect(pickers.chosen("indian-state")).toHaveText("25");

    await pickers.documentDate.fill("31-02-2020");
    await state.focus();
    await expect(pickers.form.getByText(notADate, { exact: true })).toBeVisible();
    await expect(pickers.documentDate).toHaveAttribute("aria-invalid", "true");
    await expect(pickers.chosen("document-date")).toHaveText("None");
    await expect(state).toHaveValue("25 (name not available)");
  });

  test("offers the periods of the chosen financial year and clears the period when the year changes", async ({
    page,
  }) => {
    const years = await readFinancialYears(page.request, "2025-26", "2026-27");
    const first = years.find((year) => year.label === "2025-26");
    const second = years.find((year) => year.label === "2026-27");
    expect(first, "the financial year 2025-26").toBeDefined();
    expect(second, "the financial year 2026-27").toBeDefined();
    const pickers = new ReferencePickersPage(page);
    await pickers.goto();
    const period = pickers.picker("Period");
    const periods = pickers.listbox("Period");

    await period.click();
    await expect(pickers.statusOf("Period")).toHaveText("Choose a financial year first.");
    await period.press("Escape");

    await pickers.chooseByKeyboard("Financial year", "2025-26", "2025-26");
    await period.focus();
    await period.press("ArrowDown");
    await expect(periods.getByRole("option")).toHaveCount(first!.periods.length);
    expect(await pickers.optionLabels("Period")).toEqual(first!.periods.map((item) => item.label));
    await period.press("Enter");
    await expect(periods).toBeHidden();
    await expect(period).toHaveValue(first!.periods[0]!.label);
    await expect(pickers.chosen("period")).toHaveText(first!.periods[0]!.start);

    await pickers.chooseByKeyboard("Financial year", "2026-27", "2026-27");
    await expect(pickers.chosen("financial-year")).toHaveText("2026-27");
    await expect(pickers.chosen("period")).toHaveText("None");
    await expect(period).toHaveValue("");
    await period.focus();
    await period.press("ArrowDown");
    await expect(periods.getByRole("option")).toHaveCount(second!.periods.length);
    expect(await pickers.optionLabels("Period")).toEqual(second!.periods.map((item) => item.label));
    await period.press("Escape");
    await expect(periods).toBeHidden();
  });
});

test.describe("reference pickers without the API", () => {
  test.skip(
    !offlineBaseURL,
    "E2E_OFFLINE_BASE_URL is not set and Playwright did not start the offline web instance",
  );
  test.use({
    baseURL: offlineBaseURL ?? "",
    persona: null,
    expectedConsoleError: /the server responded with a status of 500/,
  });
  test.beforeEach(async ({ context }) => {
    await addUnverifiedSessionCookie(context, offlineBaseURL ?? "");
  });

  forEachTheme("say that each list could not be read", async ({ page, capture }) => {
    const pickers = new ReferencePickersPage(page);
    const renewal = sessionRenewed(page);
    await pickers.goto();
    expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);
    await capture("reference-pickers-unavailable", pickers.card);

    const messages: Readonly<Record<PickerName, string>> = {
      Country: "The list of countries could not be read. Try again later.",
      "State or union territory":
        "The list of Indian states and union territories could not be read. Try again later.",
      Currency: "The list of currencies could not be read. Try again later.",
      "Financial year": "The list of financial years could not be read. Try again later.",
      Period: "Choose a financial year first.",
    };
    for (const [name, message] of Object.entries(messages) as [PickerName, string][]) {
      const picker = pickers.picker(name);
      await picker.click();
      await expect(pickers.statusOf(name), name).toHaveText(message);
      await expect(picker, name).toHaveAttribute("aria-expanded", "false");
      await picker.press("Escape");
    }
  });
});

test.describe("reference pickers with the reference data module disabled", () => {
  test.skip(
    !gatedBaseURL,
    "E2E_GATED_BASE_URL is not set and Playwright did not start the gated web instance",
  );
  test.use({ baseURL: gatedBaseURL ?? "" });

  test("say that the reference lists are switched off", async ({ page }) => {
    const pickers = new ReferencePickersPage(page);
    await pickers.goto();

    for (const name of listPickers) {
      const picker = pickers.picker(name);
      await picker.click();
      await expect(pickers.statusOf(name), name).toHaveText("The reference lists are switched off.");
      await picker.press("Escape");
    }
  });
});
