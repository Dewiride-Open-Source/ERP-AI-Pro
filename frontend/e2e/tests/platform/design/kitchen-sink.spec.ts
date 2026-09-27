import type { Locator, Page } from "@playwright/test";

import { expect, forEachTheme, pressArrowUntilChecked, test } from "../../../fixtures/test";
import { LoginPage } from "../../../pages/identity/auth/login.page";
import { KitchenSinkPage, kitchenSinkSections } from "../../../pages/platform/design/kitchen-sink.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

const tokenSections = kitchenSinkSections.filter((section) => section.group === "tokens");

const primitiveSections = kitchenSinkSections.filter((section) => section.group === "primitives");

const reducedMotionSeconds = 0.01 / 1000;

const narrowestScreen = { width: 320, height: 720 };

const typeRoles = [
  { role: "title", fontSize: 30, lineHeight: 36, fontWeight: "600", letterSpacing: -0.75 },
  { role: "heading", fontSize: 20, lineHeight: 28, fontWeight: "600", letterSpacing: -0.5 },
  { role: "body", fontSize: 14, lineHeight: 20, fontWeight: "400", letterSpacing: 0 },
  { role: "caption", fontSize: 12, lineHeight: 16, fontWeight: "400", letterSpacing: 0 },
  { role: "eyebrow", fontSize: 12, lineHeight: 16, fontWeight: "600", letterSpacing: 1.2 },
] as const;

const shadowTokens = ["2xs", "xs", "sm", "md", "lg", "xl", "2xl"] as const;

const menubarMenus = ["File", "Edit", "View", "Taxes", "Help"] as const;

const pngSignature = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

type ChosenFile = { name: string; mimeType: string; buffer: Buffer };

type MenuItemChoice = { name: string; variant: "default" | "destructive" };

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

async function overflowingTables(page: Page): Promise<string[]> {
  return page.getByRole("table").evaluateAll((tables) =>
    tables
      .filter((table) => {
        const container = table.parentElement;
        const wider = container !== null && container.scrollWidth > container.clientWidth;
        return wider || table.getBoundingClientRect().right > document.documentElement.clientWidth;
      })
      .map((table) => table.querySelector("caption")?.textContent ?? table.outerHTML.slice(0, 80)),
  );
}

async function expectInsideScreen(menu: Locator): Promise<void> {
  await expect(menu).toBeVisible();
  // Radix rounds a menu's position to whole device pixels while its width stays fractional, so the edge of a menu that fits
  // can sit a fraction of a pixel past the screen; the edges are read once the opening zoom, which starts smaller, has ended.
  const edges = await menu.evaluate(async (element) => {
    await Promise.all(
      element.getAnimations({ subtree: true }).map((animation) => animation.finished.catch(() => undefined)),
    );
    const bounds = element.getBoundingClientRect();
    return {
      left: Math.round(bounds.left),
      right: Math.round(bounds.right),
      screen: document.documentElement.clientWidth,
    };
  });
  expect(edges.left, "left edge").toBeGreaterThanOrEqual(0);
  expect(edges.right, "right edge").toBeLessThanOrEqual(edges.screen);
}

async function ringColour(page: Page): Promise<string> {
  return page.evaluate(() => {
    const probe = document.createElement("span");
    probe.style.color = "var(--ring)";
    document.body.append(probe);
    const colour = getComputedStyle(probe).color;
    probe.remove();
    return colour;
  });
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

async function tabOntoLink(page: Page, link: Locator): Promise<void> {
  await link.focus();
  await page.keyboard.press("Shift+Tab");
  await expect(link).not.toBeFocused();
  // WebKit leaves links out of the Tab order unless Safari's "Press Tab to highlight each item" is on, so there the link is
  // focused from script right after a key press, which :focus-visible treats as keyboard focus.
  if (page.context().browser()?.browserType().name() === "webkit") await link.focus();
  else await page.keyboard.press("Tab");
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

async function chooseMenuItem(menu: Locator, { name, variant }: MenuItemChoice): Promise<void> {
  const item = menu.getByRole("menuitem", { name });
  await expect(item).toHaveAttribute("data-variant", variant);
  await item.click();
  await expect(menu).toBeHidden();
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
  test.beforeEach(({ browserName }) => {
    test.slow(
      browserName === "webkit",
      "WebKit takes about twice as long per action, and each test here drives a whole section of controls",
    );
  });

  test("triples the timeout on WebKit only", ({ browserName }) => {
    expect(test.info().timeout).toBe(test.info().project.timeout * (browserName === "webkit" ? 3 : 1));
  });

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
            - link "Focus":
              - /url: "#focus"
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

    await expect(kitchenSink.section("focus")).toMatchAriaSnapshot(`
      - region "Focus":
        - heading "Focus" [level=2]
        - heading "Tokens and utility" [level=3]
        - term: "--focus-ring-width"
        - definition: 2px · Thickness of the outline
        - term: "--focus-ring-offset"
        - definition: 2px · Gap between the element and the outline
        - term: focus-ring
        - definition: /^Draws the outline on :focus-visible in the ring colour/
        - heading "Sample" [level=3]
        - link "Link with the focus ring":
          - /url: "#focus"
        - text: Focus ring preview
    `);

    for (const { id } of tokenSections) {
      await expect(kitchenSink.section(id)).toBeVisible();
      await capture(`section-${id}`, kitchenSink.section(id));
    }

    await tabOntoLink(page, kitchenSink.focusSample);
    await expect(kitchenSink.focusSample).toBeFocused();
    await expect(kitchenSink.focusSample).toHaveCSS("outline-style", "solid");
    await expect(kitchenSink.focusSample).toHaveCSS("outline-width", "2px");
    await expect(kitchenSink.focusSample).toHaveCSS("outline-offset", "2px");
    await expect(kitchenSink.focusSample).toHaveCSS("outline-color", await ringColour(page));
    await capture("focus-ring-keyboard", kitchenSink.section("focus"));

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
    const loadingButton = actions.getByRole("button", { name: "Saving", exact: true });
    await expect(loadingButton).toBeDisabled();
    await expect(loadingButton).toHaveAttribute("aria-busy", "true");
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
    await expect(
      selection.getByRole("group", { name: "Invoice amount (₹ thousand)" }),
    ).toHaveAccessibleDescription("From ₹20,000 to ₹80,000.");

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
    await expect(excluded.getByRole("columnheader")).toHaveText([
      "Item",
      "Why it is not installed, and where it is decided",
    ]);
    await expect(excluded.getByRole("row")).toHaveCount(15);
    await expect(excluded.getByRole("row", { name: /^sidebar / })).toContainText(
      "Decided in Authenticated application shell",
    );
    await expect(excluded.getByRole("row", { name: /^sidebar / })).toContainText(
      "authentication-authenticated-application-shell",
    );
    await expect(excluded.getByRole("row", { name: /^combobox / })).toContainText("@base-ui/react");
    await expect(excluded.getByRole("row", { name: /^calendar, date-picker / })).toContainText(
      "calendar inside a popover",
    );
    await expect(excluded.getByRole("row", { name: /^toast / })).toContainText("sonner");
    await expect(excluded.getByRole("row", { name: /^toast / })).toContainText(
      "web-foundation-design-tokens-and-theme-package",
    );
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
          - listitem:
            - text: Acme Private Limited
            - paragraph: GSTIN 29AAACA1234A1Z5 · Bengaluru
            - button "Open"
          - listitem:
            - text: INV-2026-00042
            - paragraph: Due on 26 Oct 2026
            - text: Sent
          - listitem:
            - link "Link item The whole item is one link.":
              - /url: "#data-display"
        - heading "Accordion" [level=3]
        - heading "Payment terms" [level=4]:
          - button "Payment terms" [expanded]
        - region "Payment terms": Payment is due within 30 days of the invoice date.
        - heading "Bank details" [level=4]:
          - button "Bank details"
        - heading "Late fee (not configured)" [level=4]:
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
      for (const [field, value] of [
        ["Name", "Priya S."],
        ["Email", "priya.s@example.com"],
      ] as const) {
        await dialog.getByRole("textbox", { name: field }).fill(value);
        await expect(dialog.getByRole("textbox", { name: field })).toHaveValue(value);
      }
      await dialog.getByRole("button", { name: "Cancel" }).click();
      await expect(dialog).toBeHidden();
      await expect(dialogTrigger).toBeFocused();
      await dialogTrigger.click();
      await dialog.getByRole("button", { name: "Close" }).click();
      await expect(dialog).toBeHidden();
      await expect(dialogTrigger).toBeFocused();
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
      await filters.getByRole("textbox", { name: "Client" }).fill("Acme");
      await expect(filters.getByRole("textbox", { name: "Client" })).toHaveValue("Acme");
      await filters.getByRole("button", { name: "Close" }).click();
      await expect(filters).toBeHidden();
      await expect(filtersTrigger).toBeFocused();
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

      const dropdownTrigger = kitchenSink.invoiceActions;
      const dropdown = kitchenSink.invoiceActionsMenu;
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
      const downloadMenu = kitchenSink.menu("Download");
      await expect(downloadMenu).toMatchAriaSnapshot(`
      - menu "Download":
        - menuitem "PDF"
        - menuitem "E-invoice JSON"
    `);
      await capture("dropdown-submenu-open", downloadMenu);
      await downloadMenu.getByRole("menuitem", { name: "PDF" }).click();
      await expect(dropdown).toBeHidden();
      await expect(dropdownTrigger).toBeFocused();
      await dropdownTrigger.click();
      await dropdown.getByRole("menuitem", { name: "Download" }).click();
      await downloadMenu.getByRole("menuitem", { name: "E-invoice JSON" }).click();
      await expect(dropdown).toBeHidden();
      const dropdownItems: MenuItemChoice[] = [
        { name: "Edit", variant: "default" },
        { name: "Duplicate", variant: "default" },
        { name: "Delete draft", variant: "destructive" },
      ];
      for (const item of dropdownItems) {
        await dropdownTrigger.click();
        await chooseMenuItem(dropdown, item);
        await expect(dropdownTrigger).toBeFocused();
      }
      await dropdownTrigger.press("Enter");
      await expect(dropdown).toBeVisible();
      await dismissWithEscape(page, dropdown, dropdownTrigger);

      const contextTrigger = kitchenSink.contextMenuArea;
      const contextMenu = kitchenSink.contextMenu;
      await expect(contextTrigger).toHaveRole("group");
      await expect(contextTrigger).toHaveAccessibleName("Invoice row with a context menu");
      await openContextMenu(contextTrigger, contextMenu);
      await expect(contextMenu).toMatchAriaSnapshot(`
      - menu:
        - menuitem "Open Enter"
        - menuitem "Copy link Ctrl C"
        - menuitem "Move to pending approval"
        - menuitem "Move to archive"
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
      const contextItems: MenuItemChoice[] = [
        { name: "Open", variant: "default" },
        { name: "Copy link", variant: "default" },
        { name: "Move to pending approval", variant: "default" },
        { name: "Move to archive", variant: "default" },
        { name: "Remove", variant: "destructive" },
      ];
      for (const item of contextItems) {
        await chooseMenuItem(contextMenu, item);
        await openContextMenu(contextTrigger, contextMenu);
      }
      await page.keyboard.press("Escape");
      await expect(contextMenu).toBeHidden();
    });

    forEachTheme("menu bar", async ({ page, capture }) => {
      const kitchenSink = new KitchenSinkPage(page);
      await kitchenSink.goto();

      const fileTrigger = kitchenSink.menubarTrigger("File");
      const fileMenu = kitchenSink.menu("File");
      const exportMenu = kitchenSink.menu("Export");
      await fileTrigger.click();
      await fileMenu.getByRole("menuitem", { name: "Export" }).click();
      await expect(exportMenu.getByRole("menuitem", { name: "Spreadsheet" })).toBeVisible();
      await capture("menubar-open", fileMenu);
      await exportMenu.getByRole("menuitem", { name: "Spreadsheet" }).click();
      await expect(fileMenu).toBeHidden();
      await fileTrigger.click();
      await fileMenu.getByRole("menuitem", { name: "Export" }).click();
      await exportMenu.getByRole("menuitem", { name: "PDF" }).click();
      await expect(fileMenu).toBeHidden();

      await kitchenSink.menubarTrigger("Edit").click();
      await expect(kitchenSink.menu("Edit")).toMatchAriaSnapshot(`
      - menu "Edit":
        - menuitem "Undo Ctrl Z"
        - menuitem "Redo Ctrl Y"
        - separator
        - menuitem "Clear lines"
    `);
      await capture("menubar-edit-open", kitchenSink.menu("Edit"));
      await page.keyboard.press("Escape");
      await expect(kitchenSink.menu("Edit")).toBeHidden();
      const menubarItems: (MenuItemChoice & { menu: (typeof menubarMenus)[number] })[] = [
        { menu: "File", name: "New invoice", variant: "default" },
        { menu: "Edit", name: "Undo", variant: "default" },
        { menu: "Edit", name: "Redo", variant: "default" },
        { menu: "Edit", name: "Clear lines", variant: "destructive" },
        { menu: "Taxes", name: "Recalculate GST", variant: "default" },
        { menu: "Taxes", name: "Apply TDS", variant: "default" },
        { menu: "Help", name: "Keyboard shortcuts", variant: "default" },
      ];
      for (const { menu, ...item } of menubarItems) {
        await kitchenSink.menubarTrigger(menu).click();
        await chooseMenuItem(kitchenSink.menu(menu), item);
      }

      const viewTrigger = kitchenSink.menubarTrigger("View");
      const viewMenu = kitchenSink.menu("View");
      await viewTrigger.click();
      await viewMenu.getByRole("menuitemcheckbox", { name: "Show grid lines" }).click();
      await expect(viewMenu).toBeHidden();
      await viewTrigger.click();
      await expect(viewMenu.getByRole("menuitemcheckbox", { name: "Show grid lines" })).toHaveAttribute(
        "aria-checked",
        "false",
      );
      for (const zoom of ["75 %", "100 %", "125 %"]) {
        await viewMenu.getByRole("menuitemradio", { name: zoom }).click();
        await expect(viewMenu).toBeHidden();
        await viewTrigger.click();
        await expect(viewMenu.getByRole("menuitemradio", { name: zoom })).toHaveAttribute(
          "aria-checked",
          "true",
        );
      }
      await page.keyboard.press("ArrowRight");
      const taxesMenu = kitchenSink.menu("Taxes");
      await expect(taxesMenu.getByRole("menuitem", { name: "Recalculate GST" })).toBeVisible();
      await page.keyboard.press("Escape");
      await expect(taxesMenu).toBeHidden();
      await expect(kitchenSink.menubarTrigger("Taxes")).toBeFocused();
    });

    forEachTheme("navigation menu and selects", async ({ page, capture }, isMobile) => {
      const kitchenSink = new KitchenSinkPage(page);
      await kitchenSink.goto();
      // A mouse click leaves a pointer position behind, and after a scroll the browser reports that pointer leaving the
      // navigation menu, which starts Radix's close timer; a tap that opens a menu does not cancel it. So on a touch device
      // this page is tapped before anything is clicked, and the menu bar's clicks run on a page of their own.

      const navigationMenu = kitchenSink.section("navigation").getByRole("navigation", { name: "Main" });
      const tokensTrigger = navigationMenu.getByRole("button", { name: "Tokens" });
      await openNavigationMenuItem(tokensTrigger, isMobile);
      await expect(navigationMenu.getByRole("link", { name: /^Colours/ })).toBeVisible();
      await capture("navigation-menu-open", navigationMenu.locator("[data-slot='navigation-menu-viewport']"));
      await page.keyboard.press("Escape");
      await expect(navigationMenu.getByRole("link", { name: /^Colours/ })).toBeHidden();
      await expect(tokensTrigger).toHaveAttribute("aria-expanded", "false");
      const navigationLinks = [
        { group: "Primitives", link: "Actions", section: "actions" },
        { group: "Primitives", link: "Inputs", section: "inputs" },
        { group: "Primitives", link: "Overlays", section: "overlays" },
        { group: "Tokens", link: "Colours", section: "colours" },
        { group: "Tokens", link: "Typography", section: "typography" },
        { group: "Tokens", link: "Motion", section: "motion" },
      ] as const;
      for (const { group, link, section } of navigationLinks) {
        const trigger = navigationMenu.getByRole("button", { name: group });
        await openNavigationMenuItem(trigger, isMobile);
        await followNavigationMenuLink(
          navigationMenu.getByRole("link", { name: new RegExp(`^${link}`) }),
          isMobile,
        );
        await expect(page).toHaveURL(new RegExp(`#${section}$`));
        await expect(trigger).toHaveAttribute("aria-expanded", "false");
      }
      await followNavigationMenuLink(navigationMenu.getByRole("link", { name: "Excluded" }), isMobile);
      await expect(page).toHaveURL(/#excluded$/);

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

    test("reaches the context menu area and the menu bar with the keyboard", async ({ page }) => {
      const kitchenSink = new KitchenSinkPage(page);
      await kitchenSink.goto();

      await kitchenSink.invoiceActions.focus();
      await page.keyboard.press("Tab");
      await expect(kitchenSink.contextMenuArea).toBeFocused();
      await expect(kitchenSink.contextMenuArea).toHaveCSS("outline-style", "solid");
      await expect(kitchenSink.contextMenuArea).toHaveCSS("outline-width", "2px");

      await page.keyboard.press("Tab");
      const fileTrigger = kitchenSink.menubarTrigger("File");
      const editTrigger = kitchenSink.menubarTrigger("Edit");
      await expect(fileTrigger).toBeFocused();
      await expect(fileTrigger).toHaveCSS("outline-style", "solid");
      await expect(fileTrigger).toHaveCSS("outline-width", "2px");
      await page.keyboard.press("ArrowRight");
      await expect(editTrigger).toBeFocused();
      await expect(editTrigger).toHaveCSS("outline-style", "solid");
      await expect(fileTrigger).toHaveCSS("outline-style", "none");

      const editMenu = kitchenSink.menu("Edit");
      await page.keyboard.press("Enter");
      await expect(editMenu.getByRole("menuitem", { name: "Undo" })).toBeFocused();
      await page.keyboard.press("Escape");
      await expect(editMenu).toBeHidden();
      await expect(editTrigger).toBeFocused();
    });

    test("opens the context menu with the context-menu key", async ({ page, browserName }) => {
      test.skip(
        browserName !== "chromium",
        "Only Chromium turns a key press Playwright sends into a contextmenu event; Firefox and WebKit raise it from the operating system's own key handling, which synthesised keys bypass",
      );
      const kitchenSink = new KitchenSinkPage(page);
      await kitchenSink.goto();

      for (const key of ["Shift+F10", "ContextMenu"]) {
        await kitchenSink.contextMenuArea.focus();
        await page.keyboard.press(key);
        await expect(kitchenSink.contextMenu.getByRole("menuitem", { name: "Open" })).toBeFocused();
        await page.keyboard.press("ArrowDown");
        await expect(kitchenSink.contextMenu.getByRole("menuitem", { name: "Copy link" })).toBeFocused();
        await page.keyboard.press("Escape");
        await expect(kitchenSink.contextMenu).toBeHidden();
        await expect(kitchenSink.contextMenuArea).toBeFocused();
      }
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
      for (const name of ["Italic", "Underline"]) {
        await textStyle.getByRole("button", { name }).click();
        await expect(textStyle.getByRole("button", { name })).toHaveAttribute("aria-pressed", "true");
      }
      await expect(textStyle.getByRole("button", { name: "Bold" })).toHaveAttribute("aria-pressed", "true");
      await textStyle.getByRole("button", { name: "Bold" }).click();
      await expect(textStyle.getByRole("button", { name: "Bold" })).toHaveAttribute("aria-pressed", "false");
    });

    forEachTheme("inputs, including the example form's validation failure", async ({ page, capture }) => {
      const kitchenSink = new KitchenSinkPage(page);
      await kitchenSink.goto();

      const inputs = kitchenSink.section("inputs");
      const entries = [
        { role: "textbox", name: "Client name", value: "Acme Private Limited" },
        { role: "textbox", name: "GSTIN", value: "27AAACD1234E1Z5" },
        { role: "textbox", name: "Notes to the client", value: "Thank you for the order." },
        { role: "textbox", name: "Reason for the credit note", value: "The rate on line 2 was wrong." },
        { role: "textbox", name: "Amount", value: "1250.50" },
        { role: "textbox", name: "Discount", value: "10" },
        { role: "searchbox", name: "Search clients", value: "Globex" },
      ] as const;
      for (const { role, name, value } of entries) {
        const field = inputs.getByRole(role, { name, exact: true });
        await field.fill(value);
        await expect(field).toHaveValue(value);
      }
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
      const checkboxes = [
        { name: "Unchecked", checked: true },
        { name: "Checked", checked: false },
        { name: "Some rows selected", checked: true },
        { name: "I confirm the bank details are correct", checked: true },
      ] as const;
      for (const { name, checked } of checkboxes) {
        const checkbox = selection.getByRole("checkbox", { name, exact: true });
        await checkbox.click();
        await expect(checkbox).toBeChecked({ checked });
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
      const invoiceCopy = selection.getByRole("radiogroup", { name: "Invoice copy" });
      await selection.getByText("Duplicate for transporter").click();
      await expect(invoiceCopy.getByRole("radio", { name: "Duplicate for transporter" })).toBeChecked();
      await invoiceCopy.getByRole("radio", { name: "Original for recipient" }).click();
      await expect(invoiceCopy.getByRole("radio", { name: "Original for recipient" })).toBeChecked();
      await expect(invoiceCopy.getByRole("radio", { name: "Duplicate for transporter" })).not.toBeChecked();
      const switches = [
        { name: "Email notifications", checked: true },
        { name: "Compact rows", checked: false },
        { name: "Accept the data processing terms", checked: true },
      ] as const;
      for (const { name, checked } of switches) {
        const control = selection.getByRole("switch", { name });
        await control.click();
        await expect(control).toBeChecked({ checked });
      }
      await selection.getByText("Round off totals").click();
      await expect(selection.getByRole("switch", { name: "Round off totals" })).not.toBeChecked();
      const range = selection.getByRole("group", { name: "Invoice amount (₹ thousand)" });
      await expect(range).toHaveAccessibleDescription("From ₹20,000 to ₹80,000.");
      await range.getByRole("slider", { name: "Minimum" }).focus();
      await page.keyboard.press("ArrowRight");
      await expect(range.getByRole("slider", { name: "Minimum" })).toHaveAttribute("aria-valuenow", "25");
      await range.getByRole("slider", { name: "Maximum" }).focus();
      await page.keyboard.press("ArrowLeft");
      await expect(range.getByRole("slider", { name: "Maximum" })).toHaveAttribute("aria-valuenow", "75");
      await expect(range).toHaveAccessibleDescription("From ₹25,000 to ₹75,000.");
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
      for (const { tabs, tab } of [
        { tabs: defaultTabs, tab: "Overview" },
        { tabs: lineTabs, tab: "Details" },
      ]) {
        await tabs.getByRole("tab", { name: tab }).focus();
        await page.keyboard.press("Tab");
        await expect(tabs.getByRole("tabpanel")).toBeFocused();
        await expect(tabs.getByRole("tabpanel")).toHaveCSS("outline-style", "solid");
        await expect(tabs.getByRole("tabpanel")).toHaveCSS("outline-width", "2px");
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
      const columns = dataDisplay.getByRole("group", { name: "Columns to show" }).getByRole("checkbox");
      const columnCount = await columns.count();
      expect(columnCount, "column checkboxes").toBeGreaterThan(1);
      for (let index = 0; index < columnCount; index += 1) {
        const column = columns.nth(index);
        const wasChecked = await column.isChecked();
        await column.click();
        await expect(column).toBeChecked({ checked: !wasChecked });
      }
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

  test("keeps keyboard focus clear of the sticky header", async ({ page }) => {
    const kitchenSink = new KitchenSinkPage(page);
    await kitchenSink.goto();
    const header = await new AppShell(page).banner.boundingBox();
    expect(header, "header bounding box").not.toBeNull();
    const headerBottom = (header?.y ?? 0) + (header?.height ?? 0);
    const ringExtent = await page.evaluate(() => {
      const root = getComputedStyle(document.documentElement);
      return (
        Number.parseFloat(root.getPropertyValue("--focus-ring-width")) +
        Number.parseFloat(root.getPropertyValue("--focus-ring-offset"))
      );
    });
    expect(ringExtent, "focus ring width plus offset").toBeGreaterThan(0);
    const scrollPadding = Number.parseFloat(
      await html(page).evaluate((element) => getComputedStyle(element).scrollPaddingTop),
    );
    expect(scrollPadding, "scroll-padding-top").toBeGreaterThanOrEqual((header?.height ?? 0) + ringExtent);

    await kitchenSink.section("overlays").getByRole("button", { name: "Edit contact" }).focus();
    for (let stop = 1; stop <= 12; stop += 1) {
      await page.keyboard.press("Shift+Tab");
      // WebKit scrolls a newly focused element into view on a later rendering update, so the position is polled until the
      // scroll has settled; an element left under the header still fails when the poll times out.
      await expect
        .poll(
          async () =>
            (await page.evaluate(() => document.activeElement?.getBoundingClientRect().top ?? 0)) -
            ringExtent,
          { message: `focus ring of stop ${stop} before Edit contact` },
        )
        .toBeGreaterThanOrEqual(headerBottom);
    }
  });

  test.describe("on the narrowest screen", () => {
    test.use({ viewport: narrowestScreen });

    test("keeps every table and menu inside the screen", async ({ page }) => {
      const kitchenSink = new KitchenSinkPage(page);
      await kitchenSink.goto();

      expect(await hasHorizontalOverflow(page), "horizontal overflow").toBe(false);
      await expect(page.getByRole("table")).toHaveCount(4);
      expect(await overflowingTables(page), "tables wider than their container").toEqual([]);

      await kitchenSink.invoiceActions.click();
      await expectInsideScreen(kitchenSink.invoiceActionsMenu);
      await kitchenSink.invoiceActionsMenu.getByRole("menuitem", { name: "Download" }).click();
      await expectInsideScreen(kitchenSink.menu("Download"));
      await page.keyboard.press("Escape");
      await expect(kitchenSink.invoiceActionsMenu).toBeHidden();

      const area = await kitchenSink.contextMenuArea.boundingBox();
      expect(area, "context-menu area bounding box").not.toBeNull();
      for (const x of [4, (area?.width ?? 0) / 2, (area?.width ?? 0) - 4]) {
        await kitchenSink.contextMenuArea.click({
          button: "right",
          position: { x, y: (area?.height ?? 0) / 2 },
        });
        await expectInsideScreen(kitchenSink.contextMenu);
        await page.keyboard.press("Escape");
        await expect(kitchenSink.contextMenu).toBeHidden();
      }

      for (const name of menubarMenus) {
        await kitchenSink.menubarTrigger(name).click();
        await expectInsideScreen(kitchenSink.menu(name));
        if (name === "File") {
          await kitchenSink.menu(name).getByRole("menuitem", { name: "Export" }).click();
          await expectInsideScreen(kitchenSink.menu("Export"));
        }
        await page.keyboard.press("Escape");
        await expect(kitchenSink.menu(name)).toBeHidden();
      }
      expect(await hasHorizontalOverflow(page), "horizontal overflow with the menus used").toBe(false);
    });
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
