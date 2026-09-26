import type { Locator, Page } from "@playwright/test";

import { expect, forEachTheme, pressArrowUntilChecked, test } from "../../../fixtures/test";
import { LoginPage } from "../../../pages/identity/auth/login.page";
import { KitchenSinkPage, kitchenSinkSections } from "../../../pages/platform/design/kitchen-sink.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

const tokenSections = kitchenSinkSections.filter((section) => section.group === "tokens");

const primitiveSections = kitchenSinkSections.filter((section) => section.group === "primitives");

const reducedMotionSeconds = 0.01 / 1000;

const typeRoles = [
  { role: "title", fontSize: 30, lineHeight: 36, fontWeight: "600", letterSpacing: -0.75 },
  { role: "heading", fontSize: 20, lineHeight: 28, fontWeight: "600", letterSpacing: -0.5 },
  { role: "body", fontSize: 14, lineHeight: 20, fontWeight: "400", letterSpacing: 0 },
  { role: "caption", fontSize: 12, lineHeight: 16, fontWeight: "400", letterSpacing: 0 },
  { role: "eyebrow", fontSize: 12, lineHeight: 16, fontWeight: "600", letterSpacing: 1.2 },
] as const;

const shadowTokens = ["2xs", "xs", "sm", "md", "lg", "xl", "2xl"] as const;

const pngSignature = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

type ChosenFile = { name: string; mimeType: string; buffer: Buffer };

function html(page: Page): Locator {
  return page.locator("html");
}

async function expectTheme(page: Page, theme: "light" | "dark"): Promise<void> {
  if (theme === "dark") await expect(html(page)).toHaveClass(/(^|\s)dark(\s|$)/);
  else await expect(html(page)).not.toHaveClass(/(^|\s)dark(\s|$)/);
}

async function longestDurationSeconds(
  target: Locator,
  property: "animationDuration" | "transitionDuration",
): Promise<number> {
  const value = await target.evaluate((element, name) => getComputedStyle(element)[name], property);
  return Math.max(...value.split(",").map(parseSeconds));
}

function parseSeconds(value: string): number {
  const time = value.trim();
  return time.endsWith("ms") ? Number.parseFloat(time) / 1000 : Number.parseFloat(time);
}

async function typeMetrics(target: Locator) {
  return target.evaluate((element) => {
    const style = getComputedStyle(element);
    const pixels = (value: string) => (value === "normal" ? 0 : Number.parseFloat(value));
    return {
      fontSize: pixels(style.fontSize),
      lineHeight: pixels(style.lineHeight),
      fontWeight: style.fontWeight,
      letterSpacing: pixels(style.letterSpacing),
    };
  });
}

async function hasHorizontalOverflow(page: Page): Promise<boolean> {
  return page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
}

async function isTopmostAtCentre(target: Locator): Promise<boolean> {
  return target.evaluate((element) => {
    const box = element.getBoundingClientRect();
    const hit = document.elementFromPoint(box.left + box.width / 2, box.top + box.height / 2);
    return hit !== null && element.contains(hit);
  });
}

async function box(target: Locator): Promise<{ x: number; width: number }> {
  const bounds = await target.boundingBox();
  expect(bounds, "bounding box").not.toBeNull();
  return { x: bounds?.x ?? 0, width: bounds?.width ?? 0 };
}

async function expectSlidOneWidth(target: Locator, toggle: Locator): Promise<void> {
  const resting = await box(target);
  await toggle.click();
  await expect(target).toHaveAttribute("data-state", "on");
  await expect.poll(async () => (await box(target)).x).toBeCloseTo(resting.x + resting.width, 0);
}

// Radix closes a tooltip whenever the page scrolls, and a scroll dispatches its event on the next frame, so the trigger is
// brought into view and two frames are let pass before focus or the pointer opens the overlay.
async function reveal(trigger: Locator, isMobile: boolean): Promise<void> {
  await trigger.scrollIntoViewIfNeeded();
  await trigger.evaluate(
    () =>
      new Promise<void>((resolve) => {
        requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
      }),
  );
  if (isMobile) await trigger.focus();
  else await trigger.hover();
}

async function dismissWithEscape(page: Page, overlay: Locator, trigger: Locator): Promise<void> {
  await page.keyboard.press("Escape");
  await expect(overlay).toBeHidden();
  await expect(trigger).toBeFocused();
}

// A navigation menu opens on hover and closes when a mouse pointer leaves it, and a click toggles it, so a mouse click after
// the hover has opened it closes it again; a mouse user hovers and clicks, a touch user taps.
async function openNavigationMenuItem(trigger: Locator, isMobile: boolean): Promise<void> {
  if (isMobile) await trigger.tap();
  else await trigger.hover();
  await expect(trigger).toHaveAttribute("aria-expanded", "true");
}

async function followNavigationMenuLink(link: Locator, isMobile: boolean): Promise<void> {
  if (isMobile) await link.tap();
  else await link.click();
}

async function openContextMenu(trigger: Locator, menu: Locator): Promise<void> {
  await trigger.click({ button: "right" });
  await expect(menu).toBeVisible();
}

async function chooseFile(page: Page, button: Locator, file: ChosenFile): Promise<void> {
  const chooser = page.waitForEvent("filechooser");
  await button.click();
  await (await chooser).setFiles(file);
}

function png(name: string, size: number): ChosenFile {
  return {
    name,
    mimeType: "image/png",
    buffer: Buffer.concat([pngSignature, Buffer.alloc(size - pngSignature.length, 0x2a)]),
  };
}

async function clickEveryEnabledButton(container: Locator): Promise<void> {
  for (const button of await container.getByRole("button").all()) {
    if (await button.isEnabled()) await button.click();
  }
}

test.describe("design system kitchen sink", () => {
  test.slow(
    ({ browserName }) => browserName === "webkit",
    "WebKit takes about twice as long per action, and each test here drives a whole section of controls",
  );

  forEachTheme("renders every token group", async ({ page, capture, theme }, isMobile) => {
    const kitchenSink = new KitchenSinkPage(page);
    await kitchenSink.goto();

    await expect(page).toHaveTitle("Design system · ERP-AI-Pro");
    await expectTheme(page, theme);
    await expect(html(page)).toHaveCSS("color-scheme", theme);
    await expect(page.locator('meta[name="color-scheme"]')).toHaveAttribute("content", "light dark");

    const colourTokens = await kitchenSink.colourTokenNames();
    expect(colourTokens.length, "colour tokens declared on :root").toBeGreaterThan(0);
    expect(await kitchenSink.swatchTokenNames()).toEqual(colourTokens);
    expect(await kitchenSink.transparentSwatches()).toEqual([]);

    for (const { role, fontSize, lineHeight, fontWeight, letterSpacing } of typeRoles) {
      const metrics = await typeMetrics(kitchenSink.typeRole(role).getByRole("paragraph").first());
      expect(metrics.fontSize, `${role} font size`).toBeCloseTo(fontSize, 1);
      expect(metrics.lineHeight, `${role} line height`).toBeCloseTo(lineHeight, 1);
      expect(metrics.fontWeight, `${role} font weight`).toBe(fontWeight);
      expect(metrics.letterSpacing, `${role} letter spacing`).toBeCloseTo(letterSpacing, 1);
    }
    await expect(page.getByTestId(/^spacing-token-/)).toHaveCount(14);
    await expect(page.getByTestId(/^radius-token-/)).toHaveCount(8);
    for (const shadow of shadowTokens) {
      await expect(page.getByTestId(`shadow-token-${shadow}`)).not.toHaveCSS("box-shadow", "none");
    }
    const viewportWidth = page.viewportSize()?.width ?? 0;
    await expect(page.getByRole("main")).toHaveCSS("padding-left", viewportWidth >= 640 ? "24px" : "16px");
    await expect(new AppShell(page).banner).toHaveCSS("z-index", "40");
    await expect(
      page.getByRole("table", { name: "Higher layers paint above lower ones." }).getByRole("row"),
    ).toHaveCount(4);
    await expect(page.getByTestId("motion-tokens").getByRole("row")).toHaveCount(7);

    await expect(kitchenSink.index).toMatchAriaSnapshot(`
      - navigation "Design system sections":
        - paragraph: Tokens
        - list "Tokens":
          - listitem:
            - link "Colours":
              - /url: "#colours"
          - listitem:
            - link "Typography":
              - /url: "#typography"
          - listitem:
            - link "Spacing":
              - /url: "#spacing"
          - listitem:
            - link "Radius":
              - /url: "#radius"
          - listitem:
            - link "Elevation":
              - /url: "#elevation"
          - listitem:
            - link "Layers":
              - /url: "#layers"
          - listitem:
            - link "Motion":
              - /url: "#motion"
        - paragraph: Primitives
        - list "Primitives":
          - listitem:
            - link "Actions":
              - /url: "#actions"
          - listitem:
            - link "Inputs":
              - /url: "#inputs"
          - listitem:
            - link "Selection":
              - /url: "#selection"
          - listitem:
            - link "Feedback":
              - /url: "#feedback"
          - listitem:
            - link "Overlays":
              - /url: "#overlays"
          - listitem:
            - link "Menus":
              - /url: "#menus"
          - listitem:
            - link "Navigation":
              - /url: "#navigation"
          - listitem:
            - link "Data display":
              - /url: "#data-display"
          - listitem:
            - link "Composites":
              - /url: "#composites"
          - listitem:
            - link "Excluded primitives":
              - /url: "#excluded"
    `);

    for (const { id } of tokenSections) {
      await expect(kitchenSink.section(id)).toBeVisible();
      await capture(`section-${id}`, kitchenSink.section(id));
    }
    // At three device pixels per CSS pixel the phone-width page is taller than the 16,384 pixels a browser paints into one
    // capture, which leaves most of the image blank; there the section captures cover the page instead.
    if (!isMobile) await capture("page");
    expect(await hasHorizontalOverflow(page), "horizontal overflow").toBe(false);
  });

  forEachTheme("renders every primitive in its states", async ({ page, capture }, isMobile) => {
    const kitchenSink = new KitchenSinkPage(page);
    await kitchenSink.goto();

    const actions = kitchenSink.section("actions");
    await expect(actions.getByRole("button", { name: "Disabled", exact: true })).toBeDisabled();
    await expect(actions.getByRole("button", { name: "Disabled outline" })).toBeDisabled();
    await expect(actions.getByRole("button", { name: "Invalid" })).toHaveAttribute("aria-invalid", "true");
    const loadingButton = actions.getByRole("button", { name: /Saving$/ });
    await expect(loadingButton).toBeDisabled();
    await expect(loadingButton).toHaveAttribute("aria-busy", "true");
    await expect(loadingButton.getByRole("status", { name: "Loading" })).toBeVisible();
    await expect(actions.getByRole("button", { name: "Bold (disabled)" })).toBeDisabled();
    await expect(
      kitchenSink.specimen("Toggle").getByRole("button", { name: "Bold", exact: true }),
    ).toHaveAttribute("aria-pressed", "true");
    await expect(actions.getByRole("radio", { name: "Align right" })).toBeDisabled();

    const inputs = kitchenSink.section("inputs");
    await expect(inputs.getByRole("textbox", { name: "Company PAN" })).toBeDisabled();
    await expect(inputs.getByRole("textbox", { name: "GSTIN" })).toHaveAttribute("aria-invalid", "true");
    await expect(inputs.getByRole("textbox", { name: "GSTIN" })).toHaveAccessibleDescription(
      "Enter all 15 characters of the GSTIN.",
    );
    await expect(inputs.getByRole("textbox", { name: "Invoice number" })).not.toBeEditable();
    await expect(inputs.getByRole("textbox", { name: "Terms" })).toBeDisabled();
    await expect(inputs.getByRole("combobox", { name: "Place of supply" })).toBeDisabled();
    await expect(inputs.getByRole("combobox", { name: "Currency" })).toBeDisabled();
    await expect(inputs.getByRole("combobox", { name: "Tax rate" })).toHaveAttribute("aria-invalid", "true");
    await expect(inputs.getByTestId("inputs-loading-group")).toHaveAttribute("aria-busy", "true");

    const selection = kitchenSink.section("selection");
    const partlySelected = selection.getByRole("checkbox", { name: "Some rows selected" });
    await expect(partlySelected).toHaveAttribute("data-state", "indeterminate");
    await expect(partlySelected).toHaveAttribute("aria-checked", "mixed");
    await expect(selection.getByRole("checkbox", { name: "Disabled", exact: true })).toBeDisabled();
    await expect(
      selection.getByRole("checkbox", { name: "I confirm the bank details are correct" }),
    ).toHaveAttribute("aria-invalid", "true");
    await expect(selection.getByRole("radio", { name: "Net 90 days (needs approval)" })).toBeDisabled();
    await expect(selection.getByRole("switch", { name: "Reverse charge (not applicable)" })).toBeDisabled();
    for (const thumb of await selection
      .getByRole("group", { name: "Credit limit (₹ lakh)" })
      .getByRole("slider")
      .all()) {
      await expect(thumb).toBeDisabled();
    }

    const feedback = kitchenSink.section("feedback");
    for (const value of ["0", "45", "100"]) {
      await expect(feedback.getByTestId(`feedback-progress-${value}`)).toHaveAttribute(
        "aria-valuenow",
        value,
      );
    }
    await expect(feedback.getByRole("status", { name: "Loading client details" })).toHaveAttribute(
      "aria-busy",
      "true",
    );

    const navigation = kitchenSink.section("navigation");
    await expect(navigation.getByRole("tab", { name: "Payments" })).toBeDisabled();
    await expect(navigation.getByRole("link", { name: "Go to previous page" })).toHaveAttribute(
      "aria-disabled",
      "true",
    );
    await expect(
      kitchenSink.section("data-display").getByRole("button", { name: "Late fee (not configured)" }),
    ).toBeDisabled();

    const excluded = kitchenSink.excludedPrimitives;
    await expect(excluded.getByRole("row")).toHaveCount(15);
    await expect(excluded.getByRole("row", { name: /^sidebar / })).toContainText(
      "authentication-authenticated-application-shell",
    );
    await expect(excluded.getByRole("row", { name: /^combobox / })).toContainText("@base-ui/react");
    await expect(excluded.getByRole("row", { name: /^toast / })).toContainText("sonner");
    await expect(excluded.getByRole("row", { name: /^carousel / })).toContainText("Not planned");

    await expect(inputs).toMatchAriaSnapshot(`
      - region "Inputs":
        - heading "Inputs" [level=2]
        - heading "Label and input" [level=3]
        - group:
          - text: Client name
          - textbox "Client name":
            - /placeholder: Acme Private Limited
        - group:
          - text: Company PAN
          - textbox "Company PAN" [disabled]: AAACD1234E
        - group:
          - text: GSTIN
          - textbox "GSTIN" [invalid]: 27AAACD1234E1Z
          - alert: Enter all 15 characters of the GSTIN.
        - text: Invoice number
        - textbox "Invoice number": INV-2026-00042
        - heading "Textarea" [level=3]
        - group:
          - text: Notes to the client
          - textbox "Notes to the client":
            - /placeholder: Thank you for your business.
        - group:
          - text: Terms
          - textbox "Terms" [disabled]: Payment within 30 days.
        - group:
          - text: Reason for the credit note
          - textbox "Reason for the credit note" [invalid]
          - alert: Describe why the invoice is being corrected.
        - heading "Input group" [level=3]
        - group:
          - text: Amount
          - group:
            - group: ₹
            - textbox "Amount":
              - /placeholder: "0.00"
        - group:
          - text: Discount
          - group:
            - textbox "Discount" [invalid]: "120"
            - group: "%"
          - alert: A discount cannot exceed 100 %.
        - group:
          - text: Rate
          - group:
            - group: ₹
            - textbox "Rate" [disabled]: 2,500.00
        - group:
          - text: Search clients
          - group:
            - searchbox "Search clients": Acme
            - group:
              - status "Loading"
        - heading "Native select" [level=3]
        - group:
          - text: State
          - combobox "State":
            - option "Karnataka"
            - option "Maharashtra" [selected]
            - option "Rajasthan"
            - option "Tamil Nadu"
        - group:
          - text: Place of supply
          - combobox "Place of supply" [disabled]:
            - option "Karnataka" [selected]
        - group:
          - text: Billing state
          - combobox "Billing state" [invalid]:
            - option "Choose a state" [selected]
          - alert: Choose the billing state.
        - heading "Select" [level=3]
        - group:
          - text: Financial quarter
          - combobox "Financial quarter": Q2 (July–September)
        - group:
          - text: Currency
          - combobox "Currency" [disabled]: INR (₹)
        - group:
          - text: Tax rate
          - combobox "Tax rate" [invalid]: Choose a rate
          - alert: Choose the tax rate for this line.
        - heading "Field layouts" [level=3]
        - group "Delivery":
          - text: Delivery
          - paragraph: How the invoice reaches the client.
          - group:
            - checkbox "Send a copy to the accounts team" [checked]
          - text: or
          - group:
            - switch "Payment reminders"
          - group:
            - checkbox "Post to the ledger (after approval)" [disabled]
        - heading "Example form" [level=3]
        - group:
          - text: Name
          - textbox "Name"
          - paragraph: Required. Shown on documents you issue.
        - button "Save"
    `);

    await expect(selection).toMatchAriaSnapshot(`
      - region "Selection":
        - heading "Selection" [level=2]
        - heading "Checkbox" [level=3]
        - checkbox "Unchecked"
        - checkbox "Checked" [checked]
        - checkbox "Some rows selected" [checked=mixed]
        - checkbox "Disabled" [disabled]
        - checkbox "Disabled and checked" [checked] [disabled]
        - group:
          - checkbox "I confirm the bank details are correct" [invalid]
          - alert: Confirm the bank details to continue.
        - heading "Radio group" [level=3]
        - group "Payment terms":
          - radiogroup "Payment terms":
            - radio "Due on receipt"
            - radio "Net 30 days" [checked]
            - radio "Net 45 days"
            - radio "Net 90 days (needs approval)" [disabled]
        - group "Invoice copy":
          - radiogroup "Invoice copy" [invalid]:
            - radio "Original for recipient"
            - radio "Duplicate for transporter"
          - alert: Choose which copy to print.
        - heading "Switch" [level=3]
        - switch "Email notifications"
        - switch "Round off totals" [checked]
        - switch "Compact rows" [checked]
        - switch "Reverse charge (not applicable)" [disabled]
        - group:
          - switch "Accept the data processing terms" [invalid]
          - alert: Accept the terms to continue.
        - heading "Slider" [level=3]
        - group:
          - group "Invoice amount (₹ thousand)":
            - slider "Minimum"
            - slider "Maximum"
          - paragraph: From ₹20,000 to ₹80,000.
        - group:
          - group "Credit limit (₹ lakh)":
            - slider "Minimum" [disabled]
            - slider "Maximum" [disabled]
    `);

    await expect(navigation).toMatchAriaSnapshot(`
      - region "Navigation":
        - heading "Navigation" [level=2]
        - heading "Tabs" [level=3]
        - tablist:
          - tab "Overview" [selected]
          - tab "Lines"
          - tab "Payments" [disabled]
        - tabpanel "Overview": Issued on 26 Sep 2026 to Acme Private Limited.
        - tablist:
          - tab "Details" [selected]
          - tab "Activity"
          - tab "Files"
        - tabpanel "Details": Client since April 2024.
        - heading "Breadcrumb" [level=3]
        - navigation "breadcrumb":
          - list:
            - listitem:
              - link "Tokens":
                - /url: "#colours"
            - listitem
            - listitem:
              - link "Primitives":
                - /url: "#actions"
            - listitem:
              - link "Navigation" [disabled]
        - heading "Pagination" [level=3]
        - navigation "pagination":
          - list:
            - listitem:
              - link "Go to previous page" [disabled]
            - listitem:
              - link "1"
            - listitem:
              - link "2"
            - listitem
            - listitem:
              - link "5"
            - listitem:
              - link "Go to next page"
        - paragraph: Page 1 of 5
        - heading "Navigation menu" [level=3]
        - navigation "Main":
          - list:
            - listitem:
              - button "Tokens"
            - listitem:
              - button "Primitives"
            - listitem:
              - link "Excluded":
                - /url: "#excluded"
    `);

    await expect(kitchenSink.section("data-display")).toMatchAriaSnapshot(`
      - region "Data display":
        - heading "Data display" [level=2]
        - heading "Card" [level=3]
        - text: Receivables Outstanding across all clients.
        - button "View all"
        - paragraph: ₹2,00,600.00
        - text: Overdue More than 30 days past the due date. 3 invoices
        - heading "Table" [level=3]
        - table "Invoices issued in September 2026":
          - caption: Invoices issued in September 2026
          - rowgroup:
            - row "Invoice Client Amount"
          - rowgroup:
            - row "INV-040 Acme ₹59,000.00"
            - row "INV-041 Globex ₹1,18,000.00"
            - row "INV-042 Initech ₹23,600.00"
          - rowgroup:
            - row "Total ₹2,00,600.00"
        - heading "Avatar" [level=3]
        - img "Dewiride"
        - heading "Item" [level=3]
        - list:
          - text: Acme Private Limited
          - paragraph: GSTIN 29AAACA1234A1Z5 · Bengaluru
          - button "Open"
          - text: INV-2026-00042
          - paragraph: Due on 26 Oct 2026
          - text: Sent
          - link "Link item The whole item is one link.":
            - /url: "#data-display"
        - heading "Accordion" [level=3]
        - heading "Payment terms" [level=3]:
          - button "Payment terms" [expanded]
        - region "Payment terms": Payment is due within 30 days of the invoice date.
        - heading "Bank details" [level=3]:
          - button "Bank details"
        - heading "Late fee (not configured)" [level=3]:
          - button "Late fee (not configured)" [disabled]
        - heading "Collapsible and separator" [level=3]
        - button "Show more addresses"
        - paragraph: Registered office, Bengaluru
        - heading "Scroll area" [level=3]
        - group "Columns to show":
          - checkbox "Invoice number" [checked]
          - checkbox "Taxable value" [checked]
          - checkbox "CGST"
          - checkbox "Created by"
        - button "Q1 2025–26"
        - button "Q2 2026–27"
        - heading "Aspect ratio" [level=3]
        - text: "16 : 9"
    `);

    const buttons = kitchenSink.specimen("Button");
    await buttons.getByRole("button", { name: "Default · xs" }).focus();
    await page.keyboard.press("Tab");
    await expect(buttons.getByRole("button", { name: "Default · sm" })).toBeFocused();
    await capture("button-focus", buttons);

    const labelAndInput = kitchenSink.specimen("Label and input");
    await labelAndInput.getByRole("textbox", { name: "GSTIN" }).focus();
    await page.keyboard.press("Shift+Tab");
    await expect(labelAndInput.getByRole("textbox", { name: "Client name" })).toBeFocused();
    await capture("input-focus", labelAndInput);

    if (!isMobile) {
      await buttons.getByRole("button", { name: "Outline · default" }).hover();
      await capture("button-hover", buttons);
    }

    for (const { id } of primitiveSections) {
      await capture(`section-${id}`, kitchenSink.section(id));
    }
    expect(await hasHorizontalOverflow(page), "horizontal overflow").toBe(false);
  });

  test.describe("opens every overlay", () => {
    forEachTheme("dialogs, confirmations and sheets", async ({ page, capture }) => {
      const kitchenSink = new KitchenSinkPage(page);
      await kitchenSink.goto();
      const overlays = kitchenSink.section("overlays");

      const dialogTrigger = overlays.getByRole("button", { name: "Edit contact" });
      const dialog = page.getByRole("dialog", { name: "Edit contact" });
      await dialogTrigger.click();
      await expect(dialog).toMatchAriaSnapshot(`
      - dialog "Edit contact":
        - heading "Edit contact" [level=2]
        - paragraph: Changes apply to invoices issued from now on.
        - group:
          - text: Name
          - textbox "Name": Priya Sharma
        - group:
          - text: Email
          - textbox "Email": priya@example.com
        - button "Cancel"
        - button "Save"
        - button "Close"
    `);
      await capture("dialog-open", dialog);
      await dismissWithEscape(page, dialog, dialogTrigger);
      await dialogTrigger.click();
      await dialog.getByRole("button", { name: "Save" }).click();
      await expect(dialog).toBeHidden();
      await expect(kitchenSink.toast("Contact saved")).toBeVisible();

      const alertTrigger = overlays.getByRole("button", { name: "Delete draft" });
      const alertDialog = page.getByRole("alertdialog", { name: "Delete this draft invoice?" });
      await alertTrigger.click();
      await expect(alertDialog).toMatchAriaSnapshot(`
      - alertdialog "Delete this draft invoice?":
        - heading "Delete this draft invoice?" [level=2]
        - paragraph: The draft has no number yet, so deleting it leaves no gap in the invoice series.
        - button "Keep it"
        - button "Delete"
    `);
      await capture("alert-dialog-open", alertDialog);
      await alertDialog.getByRole("button", { name: "Keep it" }).click();
      await expect(alertDialog).toBeHidden();
      await expect(alertTrigger).toBeFocused();
      await alertTrigger.click();
      await dismissWithEscape(page, alertDialog, alertTrigger);
      await alertTrigger.click();
      await alertDialog.getByRole("button", { name: "Delete", exact: true }).click();
      await expect(alertDialog).toBeHidden();
      await expect(kitchenSink.toast("Draft deleted")).toBeVisible();

      const filtersTrigger = overlays.getByRole("button", { name: "Open filters" });
      const filters = page.getByRole("dialog", { name: "Filters" });
      await filtersTrigger.click();
      await expect(filters).toMatchAriaSnapshot(`
      - dialog "Filters":
        - heading "Filters" [level=2]
        - paragraph: Narrow the invoice list.
        - group:
          - text: Client
          - textbox "Client":
            - /placeholder: Any client
        - button "Apply"
        - button "Close"
    `);
      await capture("sheet-right-open", filters);
      await dismissWithEscape(page, filters, filtersTrigger);
      await filtersTrigger.click();
      await filters.getByRole("button", { name: "Apply" }).click();
      await expect(filters).toBeHidden();
      await expect(filtersTrigger).toBeFocused();

      const totalsTrigger = overlays.getByRole("button", { name: "Show totals" });
      const totals = page.getByRole("dialog", { name: "Totals" });
      await totalsTrigger.click();
      await expect(totals).toMatchAriaSnapshot(`
      - dialog "Totals":
        - heading "Totals" [level=2]
        - paragraph: Taxable value ₹1,00,000.00 · GST ₹18,000.00 · Total ₹1,18,000.00
        - button "Close"
    `);
      await capture("sheet-bottom-open", totals);
      await dismissWithEscape(page, totals, totalsTrigger);
      await totalsTrigger.click();
      await totals.getByRole("button", { name: "Close" }).click();
      await expect(totals).toBeHidden();
      await expect(totalsTrigger).toBeFocused();
    });

    forEachTheme("popovers, hover cards and tooltips", async ({ page, capture }, isMobile) => {
      const kitchenSink = new KitchenSinkPage(page);
      await kitchenSink.goto();
      const overlays = kitchenSink.section("overlays");

      const popoverTrigger = overlays.getByRole("button", { name: "Tax breakdown" });
      const popover = page.getByRole("dialog", { name: "Tax breakdown" });
      await popoverTrigger.click();
      await expect(popover).toMatchAriaSnapshot(`
      - dialog "Tax breakdown":
        - text: Tax breakdown
        - paragraph: Intra-state supply, so GST splits into CGST and SGST.
        - term: CGST 9 %
        - definition: ₹9,000.00
        - term: SGST 9 %
        - definition: ₹9,000.00
    `);
      await expect(popover).toHaveAccessibleDescription(
        "Intra-state supply, so GST splits into CGST and SGST.",
      );
      await capture("popover-open", popover);
      await dismissWithEscape(page, popover, popoverTrigger);

      const hoverCardTrigger = overlays.getByRole("button", { name: "Acme Private Limited" });
      const hoverCard = page.getByTestId("overlay-hover-card");
      await reveal(hoverCardTrigger, isMobile);
      await expect(hoverCard).toMatchAriaSnapshot(`
      - text: AP
      - paragraph: Acme Private Limited
      - paragraph: Client since April 2024 · Bengaluru
    `);
      await capture("hover-card-open", hoverCard);
      await page.keyboard.press("Escape");
      await expect(hoverCard).toBeHidden();

      const tooltipTrigger = overlays.getByRole("button", { name: "Save", exact: true });
      const tooltip = page.getByRole("tooltip");
      await reveal(tooltipTrigger, isMobile);
      await expect(tooltip).toHaveText("Save Ctrl S");
      await capture("tooltip-open", page.getByTestId("overlay-tooltip"));
      await page.keyboard.press("Escape");
      await expect(tooltip).toBeHidden();
    });
  });

  test.describe("opens every menu", () => {
    forEachTheme("dropdown and context menus", async ({ page, capture }) => {
      const kitchenSink = new KitchenSinkPage(page);
      await kitchenSink.goto();
      const menus = kitchenSink.section("menus");

      const dropdownTrigger = menus.getByRole("button", { name: "Invoice actions", exact: true });
      const dropdown = page.getByRole("menu", { name: "Invoice actions", exact: true });
      await dropdownTrigger.click();
      await expect(dropdown).toMatchAriaSnapshot(`
      - menu "Invoice actions":
        - text: INV-2026-00042
        - group:
          - menuitem "Edit Ctrl E"
          - menuitem "Duplicate Ctrl D"
          - menuitem "Download"
          - menuitem "Cancel e-invoice (after 24 hours)" [disabled]
        - separator
        - menuitemcheckbox "Show paid invoices" [checked]
        - separator
        - text: Sort by
        - group:
          - menuitemradio "Newest first" [checked]
          - menuitemradio "Due date"
          - menuitemradio "Amount"
        - separator
        - menuitem "Delete draft"
    `);
      await capture("dropdown-open", dropdown);
      await dropdown.getByRole("menuitemcheckbox", { name: "Show paid invoices" }).click();
      await expect(dropdown).toBeHidden();
      await dropdownTrigger.click();
      await expect(dropdown.getByRole("menuitemcheckbox", { name: "Show paid invoices" })).toHaveAttribute(
        "aria-checked",
        "false",
      );
      await dropdown.getByRole("menuitemradio", { name: "Due date" }).click();
      await expect(dropdown).toBeHidden();
      await dropdownTrigger.click();
      await expect(dropdown.getByRole("menuitemradio", { name: "Due date" })).toHaveAttribute(
        "aria-checked",
        "true",
      );
      await dropdown.getByRole("menuitem", { name: "Download" }).click();
      const downloadMenu = page.getByRole("menu", { name: "Download" });
      await expect(downloadMenu.getByRole("menuitem", { name: "PDF" })).toBeVisible();
      await capture("dropdown-submenu-open", downloadMenu);
      await downloadMenu.getByRole("menuitem", { name: "PDF" }).click();
      await expect(dropdown).toBeHidden();
      await expect(dropdownTrigger).toBeFocused();
      await dropdownTrigger.press("Enter");
      await expect(dropdown).toBeVisible();
      await dismissWithEscape(page, dropdown, dropdownTrigger);

      const contextTrigger = menus.getByTestId("menus-context-trigger");
      const contextMenu = page.getByTestId("menus-context");
      await openContextMenu(contextTrigger, contextMenu);
      await expect(contextMenu).toMatchAriaSnapshot(`
      - menu:
        - menuitem "Open Enter"
        - menuitem "Copy link Ctrl C"
        - menuitem "Move to"
        - menuitem "Restore (nothing deleted)" [disabled]
        - separator
        - menuitemcheckbox "Pin to top"
        - separator
        - text: Row density
        - group:
          - menuitemradio "Comfortable" [checked]
          - menuitemradio "Compact"
        - separator
        - menuitem "Remove"
    `);
      await capture("context-menu-open", contextMenu);
      await contextMenu.getByRole("menuitemcheckbox", { name: "Pin to top" }).click();
      await expect(contextMenu).toBeHidden();
      await openContextMenu(contextTrigger, contextMenu);
      await expect(contextMenu.getByRole("menuitemcheckbox", { name: "Pin to top" })).toHaveAttribute(
        "aria-checked",
        "true",
      );
      await contextMenu.getByRole("menuitemradio", { name: "Compact" }).click();
      await expect(contextMenu).toBeHidden();
      await openContextMenu(contextTrigger, contextMenu);
      await expect(contextMenu.getByRole("menuitemradio", { name: "Compact" })).toHaveAttribute(
        "aria-checked",
        "true",
      );
      await contextMenu.getByRole("menuitem", { name: "Move to" }).click();
      const moveMenu = page.getByRole("menu", { name: "Move to" });
      await moveMenu.getByRole("menuitem", { name: "Archived" }).click();
      await expect(contextMenu).toBeHidden();
      await openContextMenu(contextTrigger, contextMenu);
      await page.keyboard.press("Escape");
      await expect(contextMenu).toBeHidden();
    });

    forEachTheme("menu bar, navigation menu and selects", async ({ page, capture }, isMobile) => {
      const kitchenSink = new KitchenSinkPage(page);
      await kitchenSink.goto();

      const menubar = page.getByRole("menubar");
      const fileMenu = page.getByRole("menu", { name: "File", exact: true });
      await menubar.getByRole("menuitem", { name: "File", exact: true }).click();
      await fileMenu.getByRole("menuitem", { name: "Export" }).click();
      const exportMenu = page.getByRole("menu", { name: "Export" });
      await expect(exportMenu.getByRole("menuitem", { name: "Spreadsheet" })).toBeVisible();
      await capture("menubar-open", fileMenu);
      await exportMenu.getByRole("menuitem", { name: "Spreadsheet" }).click();
      await expect(fileMenu).toBeHidden();

      const viewTrigger = menubar.getByRole("menuitem", { name: "View", exact: true });
      const viewMenu = page.getByRole("menu", { name: "View", exact: true });
      await viewTrigger.click();
      await viewMenu.getByRole("menuitemcheckbox", { name: "Show grid lines" }).click();
      await expect(viewMenu).toBeHidden();
      await viewTrigger.click();
      await expect(viewMenu.getByRole("menuitemcheckbox", { name: "Show grid lines" })).toHaveAttribute(
        "aria-checked",
        "false",
      );
      await viewMenu.getByRole("menuitemradio", { name: "125 %" }).click();
      await expect(viewMenu).toBeHidden();
      await viewTrigger.click();
      await expect(viewMenu.getByRole("menuitemradio", { name: "125 %" })).toHaveAttribute(
        "aria-checked",
        "true",
      );
      await page.keyboard.press("ArrowRight");
      const taxesMenu = page.getByRole("menu", { name: "Taxes" });
      await expect(taxesMenu.getByRole("menuitem", { name: "Recalculate GST" })).toBeVisible();
      await page.keyboard.press("Escape");
      await expect(taxesMenu).toBeHidden();
      await expect(menubar.getByRole("menuitem", { name: "Taxes" })).toBeFocused();

      const navigationMenu = kitchenSink.section("navigation").getByRole("navigation", { name: "Main" });
      const tokensTrigger = navigationMenu.getByRole("button", { name: "Tokens" });
      await openNavigationMenuItem(tokensTrigger, isMobile);
      await expect(navigationMenu.getByRole("link", { name: /^Colours/ })).toBeVisible();
      await capture("navigation-menu-open", navigationMenu.locator("[data-slot='navigation-menu-viewport']"));
      await page.keyboard.press("Escape");
      await expect(navigationMenu.getByRole("link", { name: /^Colours/ })).toBeHidden();
      await expect(tokensTrigger).toHaveAttribute("aria-expanded", "false");
      await openNavigationMenuItem(navigationMenu.getByRole("button", { name: "Primitives" }), isMobile);
      await followNavigationMenuLink(navigationMenu.getByRole("link", { name: /^Overlays/ }), isMobile);
      await expect(page).toHaveURL(/#overlays$/);

      const inputs = kitchenSink.section("inputs");
      const quarter = inputs.getByRole("combobox", { name: "Financial quarter" });
      const listbox = page.getByRole("listbox");
      await quarter.click();
      await expect(listbox.getByRole("option", { name: "Q4 (January–March)", disabled: true })).toHaveCount(
        1,
      );
      await capture("select-open", listbox);
      await listbox.getByRole("option", { name: "Q3 (October–December)" }).click();
      await expect(listbox).toBeHidden();
      await expect(quarter).toHaveText("Q3 (October–December)");
      const taxRate = inputs.getByRole("combobox", { name: "Tax rate" });
      await taxRate.click();
      await listbox.getByRole("option", { name: "GST 18 %" }).click();
      await expect(taxRate).toHaveText("GST 18 %");
    });
  });

  test.describe("operates every control", () => {
    forEachTheme("actions", async ({ page }) => {
      const kitchenSink = new KitchenSinkPage(page);
      await kitchenSink.goto();

      for (const title of ["Button", "Button states", "Button group"]) {
        await clickEveryEnabledButton(kitchenSink.specimen(title));
      }

      const toggles = kitchenSink.specimen("Toggle");
      for (const name of ["Bold", "Italic", "Underline"]) {
        const toggle = toggles.getByRole("button", { name, exact: true });
        const pressed = await toggle.getAttribute("aria-pressed");
        await toggle.click();
        await expect(toggle).toHaveAttribute("aria-pressed", pressed === "true" ? "false" : "true");
      }
      const toggleGroups = kitchenSink.specimen("Toggle group");
      await toggleGroups.getByRole("radio", { name: "Align centre" }).click();
      await expect(toggleGroups.getByRole("radio", { name: "Align centre" })).toBeChecked();
      await expect(toggleGroups.getByRole("radio", { name: "Align left" })).not.toBeChecked();
      const textStyle = toggleGroups.getByRole("toolbar", { name: "Text style" });
      await textStyle.getByRole("button", { name: "Italic" }).click();
      await expect(textStyle.getByRole("button", { name: "Italic" })).toHaveAttribute("aria-pressed", "true");
      await expect(textStyle.getByRole("button", { name: "Bold" })).toHaveAttribute("aria-pressed", "true");
    });

    forEachTheme("inputs, including the example form's validation failure", async ({ page, capture }) => {
      const kitchenSink = new KitchenSinkPage(page);
      await kitchenSink.goto();

      const inputs = kitchenSink.section("inputs");
      await inputs.getByRole("textbox", { name: "Client name" }).fill("Acme Private Limited");
      await expect(inputs.getByRole("textbox", { name: "Client name" })).toHaveValue("Acme Private Limited");
      await inputs.getByRole("textbox", { name: "Notes to the client" }).fill("Thank you for the order.");
      await expect(inputs.getByRole("textbox", { name: "Notes to the client" })).toHaveValue(
        "Thank you for the order.",
      );
      await inputs.getByRole("textbox", { name: "Amount" }).fill("1250.50");
      await expect(inputs.getByRole("textbox", { name: "Amount" })).toHaveValue("1250.50");
      await inputs.getByRole("combobox", { name: "State", exact: true }).selectOption("RJ");
      await expect(inputs.getByRole("combobox", { name: "State", exact: true })).toHaveValue("RJ");
      await inputs.getByRole("combobox", { name: "Billing state" }).selectOption("KA");
      await expect(inputs.getByRole("combobox", { name: "Billing state" })).toHaveValue("KA");
      await inputs.getByRole("checkbox", { name: "Send a copy to the accounts team" }).click();
      await expect(
        inputs.getByRole("checkbox", { name: "Send a copy to the accounts team" }),
      ).not.toBeChecked();
      await inputs.getByRole("switch", { name: "Payment reminders" }).click();
      await expect(inputs.getByRole("switch", { name: "Payment reminders" })).toBeChecked();

      await kitchenSink.exampleFormSubmit.click();
      await expect(kitchenSink.exampleFormName).toHaveAttribute("aria-invalid", "true");
      await expect(kitchenSink.exampleFormName).toBeFocused();
      await expect(kitchenSink.exampleForm.getByRole("alert")).toHaveText("Enter a name.");
      await expect(kitchenSink.exampleFormName).toHaveAccessibleDescription(
        "Required. Shown on documents you issue. Enter a name.",
      );
      await capture("example-form-invalid", kitchenSink.exampleForm);
      await kitchenSink.exampleFormName.fill("Priya Sharma");
      await kitchenSink.exampleFormSubmit.click();
      await expect(kitchenSink.toast("Priya Sharma was saved.")).toBeVisible();
      await expect(kitchenSink.exampleFormName).not.toHaveAttribute("aria-invalid");
      await expect(kitchenSink.exampleForm.getByRole("alert")).toHaveCount(0);
    });

    forEachTheme("selection controls and every toast", async ({ page, capture }) => {
      const kitchenSink = new KitchenSinkPage(page);
      await kitchenSink.goto();

      const selection = kitchenSink.section("selection");
      for (const name of ["Unchecked", "Some rows selected", "I confirm the bank details are correct"]) {
        await selection.getByRole("checkbox", { name }).click();
        await expect(selection.getByRole("checkbox", { name })).toBeChecked();
      }
      const paymentTerms = selection.getByRole("radiogroup", { name: "Payment terms" });
      await paymentTerms.getByRole("radio", { name: "Net 30 days" }).focus();
      await pressArrowUntilChecked(
        page,
        "ArrowDown",
        paymentTerms.getByRole("radio", { name: "Net 45 days" }),
      );
      await expect(paymentTerms.getByRole("radio", { name: "Net 45 days" })).toBeFocused();
      await pressArrowUntilChecked(
        page,
        "ArrowDown",
        paymentTerms.getByRole("radio", { name: "Due on receipt" }),
      );
      await pressArrowUntilChecked(page, "ArrowUp", paymentTerms.getByRole("radio", { name: "Net 45 days" }));
      await expect(paymentTerms.getByRole("radio", { name: "Net 30 days" })).not.toBeChecked();
      await selection.getByText("Duplicate for transporter").click();
      await expect(selection.getByRole("radio", { name: "Duplicate for transporter" })).toBeChecked();
      await selection.getByRole("switch", { name: "Email notifications" }).click();
      await expect(selection.getByRole("switch", { name: "Email notifications" })).toBeChecked();
      await selection.getByText("Round off totals").click();
      await expect(selection.getByRole("switch", { name: "Round off totals" })).not.toBeChecked();
      const range = selection.getByRole("group", { name: "Invoice amount (₹ thousand)" });
      await range.getByRole("slider", { name: "Minimum" }).focus();
      await page.keyboard.press("ArrowRight");
      await expect(range.getByRole("slider", { name: "Minimum" })).toHaveAttribute("aria-valuenow", "25");
      await range.getByRole("slider", { name: "Maximum" }).focus();
      await page.keyboard.press("ArrowLeft");
      await expect(range.getByRole("slider", { name: "Maximum" })).toHaveAttribute("aria-valuenow", "75");
      await capture("selection-operated", selection);

      const feedback = kitchenSink.section("feedback");
      const toasts = [
        { trigger: "Success", title: "Invoice sent" },
        { trigger: "Info", title: "Rates updated" },
        { trigger: "Warning", title: "Due date passed" },
        { trigger: "Error", title: "Payment failed" },
      ] as const;
      for (const { trigger, title } of toasts) {
        await feedback.getByRole("button", { name: trigger, exact: true }).click();
        await expect(kitchenSink.toast(title)).toBeVisible();
      }
      await feedback.getByRole("button", { name: "Loading", exact: true }).click();
      const exportToast = kitchenSink.toast("Preparing the export");
      await expect(exportToast).toBeVisible();
      await capture("toast-loading", exportToast);
      await exportToast.getByRole("button", { name: "Finish" }).click();
      await expect(kitchenSink.toast("Export ready")).toBeVisible();
      await expect(kitchenSink.toast("Preparing the export")).toHaveCount(0);
      await feedback.getByRole("link", { name: "link" }).click();
      await expect(page).toHaveURL(/#feedback$/);
    });

    forEachTheme("navigation and data display controls", async ({ page }) => {
      const kitchenSink = new KitchenSinkPage(page);
      await kitchenSink.goto();

      const navigation = kitchenSink.section("navigation");
      const defaultTabs = navigation.getByTestId("navigation-tabs-default");
      const lineTabs = navigation.getByTestId("navigation-tabs-line");
      const tabPanels = [
        { tabs: defaultTabs, tab: "Lines", panel: "Two lines, taxable value ₹1,00,000.00." },
        { tabs: defaultTabs, tab: "Overview", panel: "Issued on 26 Sep 2026 to Acme Private Limited." },
        { tabs: lineTabs, tab: "Activity", panel: "Invoice INV-2026-00042 was sent today." },
        { tabs: lineTabs, tab: "Files", panel: "Three signed agreements." },
        { tabs: lineTabs, tab: "Details", panel: "Client since April 2024." },
      ] as const;
      for (const { tabs, tab, panel } of tabPanels) {
        await tabs.getByRole("tab", { name: tab }).click();
        await expect(tabs.getByRole("tab", { name: tab })).toHaveAttribute("aria-selected", "true");
        await expect(tabs.getByRole("tabpanel")).toHaveText(panel);
      }
      const breadcrumb = navigation.getByRole("navigation", { name: "breadcrumb" });
      await breadcrumb.getByRole("link", { name: "Primitives" }).click();
      await expect(page).toHaveURL(/#actions$/);
      await breadcrumb.getByRole("link", { name: "Tokens" }).click();
      await expect(page).toHaveURL(/#colours$/);
      const pagination = navigation.getByRole("navigation", { name: "pagination" });
      const status = navigation.getByTestId("navigation-pagination-status");
      await pagination.getByRole("link", { name: "2", exact: true }).click();
      await expect(pagination.getByRole("link", { name: "2", exact: true })).toHaveAttribute(
        "aria-current",
        "page",
      );
      await expect(status).toHaveText("Page 2 of 5");
      await pagination.getByRole("link", { name: "Go to next page" }).click();
      await expect(pagination.getByRole("link", { name: "3", exact: true })).toHaveAttribute(
        "aria-current",
        "page",
      );
      await expect(status).toHaveText("Page 3 of 5");
      await pagination.getByRole("link", { name: "Go to previous page" }).click();
      await expect(status).toHaveText("Page 2 of 5");
      await pagination.getByRole("link", { name: "5", exact: true }).click();
      await expect(status).toHaveText("Page 5 of 5");
      await expect(pagination.getByRole("link", { name: "Go to next page" })).toHaveAttribute(
        "aria-disabled",
        "true",
      );
      await pagination.getByRole("link", { name: "1", exact: true }).click();
      await expect(status).toHaveText("Page 1 of 5");
      await expect(page).toHaveURL(/#colours$/);

      const dataDisplay = kitchenSink.section("data-display");
      for (const title of ["Card", "Item"]) {
        await clickEveryEnabledButton(kitchenSink.specimen(title));
      }
      const accordion = dataDisplay.getByTestId("data-display-accordion");
      const paymentTermsItem = accordion.getByRole("button", { name: "Payment terms" });
      const bankDetailsItem = accordion.getByRole("button", { name: "Bank details" });
      await bankDetailsItem.click();
      await expect(bankDetailsItem).toHaveAttribute("aria-expanded", "true");
      await expect(paymentTermsItem).toHaveAttribute("aria-expanded", "false");
      await expect(accordion.getByRole("region", { name: "Bank details" })).toHaveText(
        "Pay by NEFT or RTGS to the account printed on the invoice.",
      );
      await bankDetailsItem.click();
      await expect(bankDetailsItem).toHaveAttribute("aria-expanded", "false");
      await paymentTermsItem.click();
      await expect(paymentTermsItem).toHaveAttribute("aria-expanded", "true");
      const moreAddresses = dataDisplay.getByRole("button", { name: "Show more addresses" });
      await moreAddresses.click();
      await expect(moreAddresses).toHaveAttribute("aria-expanded", "true");
      await expect(dataDisplay.getByText("Branch office, Pune")).toBeVisible();
      await moreAddresses.click();
      await expect(dataDisplay.getByText("Branch office, Pune")).toBeHidden();
      await dataDisplay.getByRole("checkbox", { name: "Created by" }).click();
      await expect(dataDisplay.getByRole("checkbox", { name: "Created by" })).toBeChecked();
      await clickEveryEnabledButton(dataDisplay.getByTestId("data-display-scroll-horizontal"));
      await dataDisplay.getByRole("link", { name: /^Link item/ }).click();
      await expect(page).toHaveURL(/#data-display$/);
      await clickEveryEnabledButton(kitchenSink.specimen("Empty"));
      expect(await hasHorizontalOverflow(page), "horizontal overflow").toBe(false);
    });

    forEachTheme("the file drop zone and the theme toggle", async ({ page, capture, theme }) => {
      const kitchenSink = new KitchenSinkPage(page);
      await kitchenSink.goto();

      const composites = kitchenSink.section("composites");
      const chooseButton = composites.getByRole("button", { name: "Choose a file" });
      await chooseFile(page, chooseButton, png("logo.png", 512));
      await expect(kitchenSink.toast("logo.png passed the checks.")).toBeVisible();
      await chooseFile(page, chooseButton, {
        name: "notes.txt",
        mimeType: "text/plain",
        buffer: Buffer.from("GST"),
      });
      await expect(kitchenSink.toast("notes.txt: Only PNG images are accepted here.")).toBeVisible();
      await chooseFile(page, chooseButton, png("scan.png", 1024 * 1024 + 1));
      await expect(kitchenSink.toast("scan.png: The file is larger than 1 MiB.")).toBeVisible();

      const sectionToggle = composites.getByRole("radiogroup", { name: "Colour theme" });
      const headerToggle = new AppShell(page).banner.getByRole("radiogroup", { name: "Colour theme" });
      await sectionToggle.getByRole("radio", { name: "Dark" }).click();
      await expectTheme(page, "dark");
      await expect(headerToggle.getByRole("radio", { name: "Dark" })).toBeChecked();
      await capture("composites-dark-selected", composites);
      await sectionToggle.getByRole("radio", { name: "Light" }).click();
      await expectTheme(page, "light");
      await sectionToggle.getByRole("radio", { name: "System" }).click();
      await expect(headerToggle.getByRole("radio", { name: "System" })).toBeChecked();
      await expectTheme(page, theme);

      expect(await hasHorizontalOverflow(page), "horizontal overflow").toBe(false);
    });
  });

  test("section index links reach their sections", async ({ page }) => {
    const kitchenSink = new KitchenSinkPage(page);
    await kitchenSink.goto();

    for (const { id } of kitchenSinkSections) {
      await kitchenSink.indexLink(id).click();
      await expect(page).toHaveURL(new RegExp(`#${id}$`));
      await expect(kitchenSink.sectionHeading(id)).toBeInViewport();
      await expect
        .poll(() => isTopmostAtCentre(kitchenSink.sectionHeading(id)), { message: `${id} heading` })
        .toBe(true);
    }
  });

  test.describe("motion", () => {
    test("runs token durations when motion is allowed", async ({ page }) => {
      const login = new LoginPage(page);
      await login.goto();
      await expect(login.glows).toHaveCount(2);
      for (const glow of await login.glows.all()) {
        await expect(glow).toHaveCSS("animation-iteration-count", "infinite");
        expect(await longestDurationSeconds(glow, "animationDuration")).toBeCloseTo(6, 3);
      }

      const kitchenSink = new KitchenSinkPage(page);
      await kitchenSink.goto();
      await expect(kitchenSink.motionPreference).toHaveAttribute("data-reduced-motion", "false");
      await expect(kitchenSink.motionDuration("fast")).toHaveText("150ms");
      await expect(kitchenSink.motionDuration("normal")).toHaveText("200ms");
      await expect(kitchenSink.motionDuration("slow")).toHaveText("300ms");
      expect(await longestDurationSeconds(kitchenSink.motionAnimated, "animationDuration")).toBeCloseTo(
        0.3,
        3,
      );
      await kitchenSink.motionReplay.click();
      expect(await longestDurationSeconds(kitchenSink.motionAnimated, "animationDuration")).toBeCloseTo(
        0.3,
        3,
      );

      expect(await longestDurationSeconds(kitchenSink.motionTransitioned, "transitionDuration")).toBeCloseTo(
        0.2,
        3,
      );
      await expectSlidOneWidth(kitchenSink.motionTransitioned, kitchenSink.motionTransitionToggle);

      await kitchenSink.section("overlays").getByRole("button", { name: "Edit contact" }).click();
      const overlay = page.locator("[data-slot='dialog-overlay']");
      await expect(overlay).toBeVisible();
      expect(await longestDurationSeconds(overlay, "animationDuration")).toBeCloseTo(0.1, 3);
    });

    test("collapses animations and transitions when reduced motion is requested", async ({ page }) => {
      await page.emulateMedia({ reducedMotion: "reduce" });

      const login = new LoginPage(page);
      await login.goto();
      await expect(login.glows).toHaveCount(2);
      for (const glow of await login.glows.all()) {
        await expect(glow).toHaveCSS("animation-iteration-count", "1");
        expect(await longestDurationSeconds(glow, "animationDuration")).toBeLessThanOrEqual(
          reducedMotionSeconds,
        );
      }

      const kitchenSink = new KitchenSinkPage(page);
      await kitchenSink.goto();
      await expect(kitchenSink.motionPreference).toHaveAttribute("data-reduced-motion", "true");
      for (const name of ["fast", "normal", "slow"] as const) {
        const reading = kitchenSink.motionDuration(name);
        await expect(reading).toHaveText("0.01ms");
        const computed = (await reading.getAttribute("data-computed")) ?? "";
        expect(parseSeconds(computed), `--motion-duration-${name}`).toBeLessThanOrEqual(reducedMotionSeconds);
      }
      expect(
        await longestDurationSeconds(kitchenSink.motionAnimated, "animationDuration"),
      ).toBeLessThanOrEqual(reducedMotionSeconds);
      await kitchenSink.motionReplay.click();
      expect(
        await longestDurationSeconds(kitchenSink.motionAnimated, "animationDuration"),
      ).toBeLessThanOrEqual(reducedMotionSeconds);
      expect(
        await longestDurationSeconds(kitchenSink.motionTransitioned, "transitionDuration"),
      ).toBeLessThanOrEqual(reducedMotionSeconds);
      await expectSlidOneWidth(kitchenSink.motionTransitioned, kitchenSink.motionTransitionToggle);

      await kitchenSink.section("overlays").getByRole("button", { name: "Edit contact" }).click();
      const overlay = page.locator("[data-slot='dialog-overlay']");
      await expect(overlay).toBeVisible();
      expect(await longestDurationSeconds(overlay, "animationDuration")).toBeLessThanOrEqual(
        reducedMotionSeconds,
      );
      expect(
        await longestDurationSeconds(page.getByRole("dialog", { name: "Edit contact" }), "animationDuration"),
      ).toBeLessThanOrEqual(reducedMotionSeconds);
    });
  });
});
