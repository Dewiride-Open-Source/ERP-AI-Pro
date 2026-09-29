import type { Locator, Page } from "@playwright/test";

import { png, type FileUpload } from "../../../fixtures/files";
import { expect, forEachTheme, pressArrowUntilChecked, tabOntoLink, test } from "../../../fixtures/test";
import { LoginPage } from "../../../pages/identity/auth/login.page";
import {
  KitchenSinkPage,
  kitchenSinkSections,
  type KitchenSinkSectionId,
} from "../../../pages/platform/design/kitchen-sink.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";
import { DataTableRegion } from "../../../pages/shared/lists/data-table.page";

const tokenSections = kitchenSinkSections.filter((section) => section.group === "tokens");

const primitiveSections = kitchenSinkSections.filter((section) => section.group === "primitives");

// On a phone with a device pixel ratio of 3 these sections are taller than the 16,384 device pixels one capture holds, so
// each of their specimens is captured on its own.
const capturedBySpecimen: ReadonlySet<KitchenSinkSectionId> = new Set(["composites", "forms"]);

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

const minusSign = String.fromCodePoint(0x2212);

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

// Playwright's WebKit ends a tap that no touch handler cancels with a mouse move, press and release at the tapped point, so
// about 100 ms after a tapped link has scrolled the page WebKit reports that mouse leaving the still-closing menu, and Radix
// starts its 150 ms close timer; a menu the next tap opens before the timer fires closes again. So on a touch device the
// tap that opens the menu and the tap on its link are retried together, and the trigger is tapped only while it is closed,
// because a tap on an open trigger closes it.
async function followNavigationMenuItem(trigger: Locator, link: Locator, isMobile: boolean): Promise<void> {
  if (!isMobile) {
    await openNavigationMenuItem(trigger, isMobile);
    await link.click();
    return;
  }
  await expect(async () => {
    if ((await trigger.getAttribute("aria-expanded", { timeout: 1_000 })) !== "true") {
      await trigger.tap({ timeout: 1_000 });
    }
    await expect(trigger).toHaveAttribute("aria-expanded", "true", { timeout: 1_000 });
    await link.tap({ timeout: 1_000 });
  }).toPass();
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

async function chooseFile(page: Page, button: Locator, file: FileUpload): Promise<void> {
  const chooser = page.waitForEvent("filechooser");
  await button.click();
  await (await chooser).setFiles(file);
}

function fieldOf(container: Locator, control: Locator): Locator {
  return container.locator("[data-slot='field']").filter({ has: control });
}

async function selection(input: Locator): Promise<{ start: number | null; end: number | null }> {
  return input.evaluate((element) =>
    element instanceof HTMLInputElement
      ? { start: element.selectionStart, end: element.selectionEnd }
      : { start: null, end: null },
  );
}

// A pointer puts the caret at the character edge nearest to it, so the point returned lies three quarters into the last
// character of the given leading text. Widths are measured on a copy of the text carrying every property of the field that
// sets how wide characters are, read one by one because the computed font shorthand can be empty, and the text starts after
// the computed border and padding, because Firefox reports an input's padding in clientLeft.
async function textOffsetPoint(input: Locator, before: string): Promise<{ x: number; y: number }> {
  return input.evaluate((element, text) => {
    const style = getComputedStyle(element);
    const probe = element.ownerDocument.createElement("span");
    probe.textContent = text.slice(0, -1);
    probe.style.position = "absolute";
    probe.style.visibility = "hidden";
    probe.style.whiteSpace = "pre";
    for (const property of [
      "fontFamily",
      "fontSize",
      "fontWeight",
      "fontStyle",
      "fontStretch",
      "fontVariantNumeric",
      "fontVariantLigatures",
      "fontFeatureSettings",
      "fontVariationSettings",
      "fontKerning",
      "letterSpacing",
    ] as const) {
      probe.style[property] = style[property];
    }
    element.ownerDocument.body.append(probe);
    const leading = probe.getBoundingClientRect().width;
    probe.textContent = text;
    const whole = probe.getBoundingClientRect().width;
    probe.remove();
    const bounds = element.getBoundingClientRect();
    const textStart =
      bounds.left + Number.parseFloat(style.borderLeftWidth) + Number.parseFloat(style.paddingLeft);
    return {
      x: textStart - element.scrollLeft + leading + (whole - leading) * 0.75,
      y: bounds.top + bounds.height / 2,
    };
  }, before);
}

// Records every time the popup shows the empty message while the list is expected to hold matches, which a list that
// recomputes its content as it closes would do during a closing animation.
async function watchEmptyMessage(page: Page, message: string): Promise<() => Promise<number>> {
  await page.evaluate((text) => {
    const seen = { count: 0 };
    const observer = new MutationObserver(() => {
      const shown = [...document.querySelectorAll("[data-slot='popover-content']")].some((content) =>
        content.textContent?.includes(text),
      );
      if (shown) seen.count += 1;
    });
    observer.observe(document.body, { childList: true, subtree: true, characterData: true });
    Object.assign(window, { emptyMessageWatch: { seen, observer } });
  }, message);
  return () =>
    page.evaluate(() => {
      const watch = (
        window as unknown as { emptyMessageWatch: { seen: { count: number }; observer: MutationObserver } }
      ).emptyMessageWatch;
      watch.observer.disconnect();
      return watch.seen.count;
    });
}

async function listboxCount(page: Page): Promise<number> {
  return page.evaluate(() => document.querySelectorAll("[role='listbox']").length);
}

async function expectPendingSave(save: Locator): Promise<void> {
  await expect(save).toHaveAttribute("aria-busy", "true");
  await expect(save).toHaveAttribute("aria-disabled", "true");
  await expect(save).not.toHaveAttribute("disabled");
  await expect(save).toBeDisabled();
  await expect(save).toBeFocused();
  await expect(save).toHaveAccessibleName("Save");
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

  forEachTheme("renders every token group", async ({ page, capture, theme }) => {
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
            - link "Forms":
              - /url: "#forms"
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

    // The page is taller than the 16,384 device pixels a browser paints into one capture, so each section is captured on its
    // own, and the capture fixture fails any capture that would still be cut.
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
    await expect(excluded.getByRole("row", { name: /^combobox / })).toContainText("built in-house");
    await expect(excluded.getByRole("row", { name: /^date-picker / })).toContainText(
      "calendar inside a popover",
    );
    await expect(excluded.getByRole("row", { name: /^calendar/ })).toHaveCount(0);
    await expect(excluded.getByRole("row", { name: /^form / })).toContainText("react-hook-form");
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

    const forms = kitchenSink.section("forms");
    await expect(forms.getByRole("textbox", { name: "Legal name" })).toHaveAttribute("aria-required", "true");
    await expect(
      forms.getByRole("textbox", { name: "Email for remittance advice" }),
    ).toHaveAccessibleDescription("Enter an email address such as accounts@acme.in.");
    await expect(forms.getByRole("textbox", { name: "Invoice amount" })).toHaveValue("1,23,45,678.50");
    await expect(forms.getByRole("textbox", { name: "Rounded total" })).toHaveValue("15,00,000");
    await expect(forms.getByRole("textbox", { name: "Rate" })).toBeDisabled();
    await expect(forms.getByRole("textbox", { name: "IFSC" })).toHaveAttribute("aria-invalid", "true");
    await expect(forms.getByRole("textbox", { name: "Delivery date" })).toHaveValue("31-02-2026");
    await expect(forms.getByRole("textbox", { name: "Contract period To" })).toHaveAttribute(
      "aria-invalid",
      "true",
    );
    await expect(forms.getByRole("combobox", { name: "Branch" })).toBeDisabled();
    await expect(forms.getByRole("button", { name: "Save draft" })).toHaveAttribute("aria-busy", "true");
    await expect(forms.getByRole("button", { name: "Send for approval" })).toBeDisabled();
    await expect(forms.getByRole("button", { name: /\b20 September 2026/ }).first()).toBeDisabled();
    await expect(forms).toMatchAriaSnapshot(`
      - region "Forms":
        - heading "Forms" [level=2]
        - heading "Field frame" [level=3]
        - group:
          - text: Client name
          - textbox "Client name": Acme Private Limited
          - paragraph: Shown on every invoice.
        - group:
          - text: Legal name
          - textbox "Legal name"
          - paragraph: As printed on the PAN card.
        - group:
          - text: Email for remittance advice
          - textbox "Email for remittance advice" [invalid]: accounts@
          - alert: Enter an email address such as accounts@acme.in.
        - group:
          - text: Company PAN
          - textbox "Company PAN" [disabled]: AAACD1234E
          - paragraph: Set by the administrator.
        - group:
          - text: Payment reminders
          - paragraph: Remind the client 3 days before the due date.
          - switch "Payment reminders" [checked]
        - group:
          - text: Credit days
          - paragraph: Label and control sit side by side where the group is wide enough.
          - textbox "Credit days": "30"
        - heading "Error summary" [level=3]
        - alert:
          - text: Some details need attention.
          - list:
            - listitem: Check these details against the registration certificate.
            - listitem:
              - link "Enter the supplier's legal name.":
                - /url: "#ks-forms-legal-name"
            - listitem:
              - link "Enter an email address such as accounts@acme.in.":
                - /url: "#ks-forms-email"
          - paragraph: "Reference: 4bf92f3577b34da6a3ce929d0e0e4736"
        - alert:
          - text: The server could not finish this. Try again.
          - paragraph: "Reference: 4bf92f3577b34da6a3ce929d0e0e4736"
        - heading "Submit button" [level=3]
        - button "Save"
        - button "Finish saving" [disabled]
        - button "Save draft" [disabled]
        - button "Send for approval" [disabled]
        - heading "Amount" [level=3]
        - group:
          - text: Invoice amount
          - group:
            - group: ₹
            - textbox "Invoice amount": 1,23,45,678.50
          - paragraph: Up to 15 digits before the decimal point.
        - paragraph: "Value: 12345678.5"
        - group:
          - text: Opening balance
          - group:
            - group: ₹
            - textbox "Opening balance":
              - /placeholder: "0.00"
        - group:
          - text: Adjustment
          - group:
            - group: ₹
            - textbox "Adjustment": "-2,500.00"
        - group:
          - text: Discount
          - group:
            - group: ₹
            - textbox "Discount" [invalid]: 1,50,000.00
          - alert: Enter an amount of at most ₹1,00,000.00.
        - group:
          - text: Rate
          - group:
            - group: ₹
            - textbox "Rate" [disabled]: 2,500.00
        - group:
          - text: Rounded total
          - group:
            - group: ₹
            - textbox "Rounded total": 15,00,000
          - paragraph: Whole rupees, without paise.
        - heading "GSTIN, PAN and IFSC" [level=3]
        - group:
          - text: GSTIN
          - textbox "GSTIN"
          - paragraph: Paste it with spaces or in lower case.
        - paragraph: "Value: (empty)"
        - group:
          - text: PAN
          - textbox "PAN": AAACD1234E
        - group:
          - text: IFSC
          - textbox "IFSC" [invalid]: HDFC000123
          - alert: "Enter an 11-character IFSC: four letters, a zero, then six letters or digits."
        - group:
          - text: Branch GSTIN
          - textbox "Branch GSTIN" [disabled]: 29AAACD1234E1Z3
        - heading "Date" [level=3]
        - group:
          - text: Invoice date
          - group:
            - textbox "Invoice date":
              - /placeholder: dd-mm-yyyy
              - text: 31-03-2026
            - group:
              - button "Choose date"
          - paragraph: Within the financial year 2025–26; the calendar disables every other day.
        - paragraph: "Value: 2026-03-31"
        - group:
          - text: Due date
          - group:
            - textbox "Due date":
              - /placeholder: dd-mm-yyyy
            - group:
              - button "Choose date"
        - group:
          - text: Delivery date
          - group:
            - textbox "Delivery date" [invalid]:
              - /placeholder: dd-mm-yyyy
              - text: 31-02-2026
            - group:
              - button "Choose date"
          - alert: Enter a real date as day-month-year, for example 31-03-2026.
        - group:
          - text: Posting date
          - group:
            - textbox "Posting date" [disabled]:
              - /placeholder: dd-mm-yyyy
              - text: 01-04-2026
            - group:
              - button "Choose date" [disabled]
        - heading "Date range" [level=3]
        - group:
          - text: Statement period From
          - group:
            - textbox "Statement period From":
              - /placeholder: dd-mm-yyyy
              - text: 01-04-2026
          - text: To
          - group:
            - textbox "Statement period To":
              - /placeholder: dd-mm-yyyy
              - text: 30-06-2026
          - button "Choose dates"
        - paragraph: "Value: 2026-04-01 to 2026-06-30"
        - group:
          - text: Contract period From
          - group:
            - textbox "Contract period From":
              - /placeholder: dd-mm-yyyy
              - text: 30-06-2026
          - text: To
          - group:
            - textbox "Contract period To" [invalid]:
              - /placeholder: dd-mm-yyyy
              - text: 01-04-2026
          - button "Choose dates"
          - alert: The end date must be on or after the start date.
        - group:
          - text: Locked period From
          - group:
            - textbox "Locked period From" [disabled]:
              - /placeholder: dd-mm-yyyy
              - text: 01-04-2025
          - text: To
          - group:
            - textbox "Locked period To" [disabled]:
              - /placeholder: dd-mm-yyyy
              - text: 31-03-2026
          - button "Choose dates" [disabled]
        - heading "Combobox" [level=3]
        - group:
          - text: Expense account
          - group:
            - combobox "Expense account"
            - group:
              - button "Show options Expense account"
            - status
          - paragraph: "Accents are ignored: cafe finds Café."
        - paragraph: "Value: (none)"
        - group:
          - text: Default expense account
          - group:
            - combobox "Default expense account": Professional fees
            - group:
              - button "Clear Default expense account"
              - button "Show options Default expense account"
            - status
        - group:
          - text: Cost centre
          - group:
            - combobox "Cost centre" [invalid]
            - group:
              - button "Show options Cost centre"
            - status
          - alert: Choose a cost centre from the list.
        - group:
          - text: Branch
          - group:
            - combobox "Branch" [disabled]: Bengaluru
            - group:
              - button "Show options Branch" [disabled]
            - status
        - group:
          - text: Supplier
          - group:
            - combobox "Supplier": Globex Cloud Services
            - group:
              - button "Clear Supplier"
              - button "Show options Supplier"
            - status
        - paragraph: "Value: globex"
        - group:
          - text: Client
          - group:
            - combobox "Client": Acme
            - group:
              - button "Clear Client"
              - button "Show options Client"
            - status
        - heading "Calendar" [level=3]
        - paragraph: One day, with Sundays disabled
        - navigation "Navigation bar":
          - button "Go to the Previous Month"
          - button "Go to the Next Month"
        - status: September 2026
        - grid "September 2026"
        - paragraph: "Selected: 15-09-2026"
        - paragraph: A range of days
        - grid "September 2026"
        - paragraph: "Range: 08-09-2026 to 12-09-2026"
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
      if (!capturedBySpecimen.has(id)) {
        await capture(`section-${id}`, kitchenSink.section(id));
        continue;
      }
      for (const title of await kitchenSink
        .section(id)
        .getByRole("heading", { level: 3 })
        .allTextContents()) {
        await capture(`section-${id}-${title}`, kitchenSink.specimen(title));
      }
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
      // A mouse click, and in Playwright's WebKit a tap too, leaves a pointer position behind, and after a scroll the
      // browser reports that pointer leaving the navigation menu, which starts Radix's close timer that a tap opening a
      // menu does not cancel. So on a touch device this page is tapped before anything is clicked, the menu bar's clicks
      // run on a page of their own, and each menu is opened and its link followed as one retried step.

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
        await followNavigationMenuItem(
          trigger,
          navigationMenu.getByRole("link", { name: new RegExp(`^${link}`) }),
          isMobile,
        );
        await expect(page).toHaveURL(new RegExp(`#${section}$`));
        await expect(trigger).toHaveAttribute("aria-expanded", "false");
      }
      const excludedLink = navigationMenu.getByRole("link", { name: "Excluded" });
      if (isMobile) await excludedLink.tap();
      else await excludedLink.click();
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
      await chooseFile(page, chooseButton, png({ name: "logo.png", size: 512 }));
      await expect(kitchenSink.toast("logo.png passed the checks.")).toBeVisible();
      await chooseFile(page, chooseButton, {
        name: "notes.txt",
        mimeType: "text/plain",
        buffer: Buffer.from("GST"),
      });
      await expect(kitchenSink.toast("notes.txt: Only PNG images are accepted here.")).toBeVisible();
      await chooseFile(page, chooseButton, png({ name: "scan.png", size: 1024 * 1024 + 1 }));
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

    forEachTheme(
      "the data table with its sorting, pages, selection and columns",
      async ({ page, capture }) => {
        const kitchenSink = new KitchenSinkPage(page);
        await kitchenSink.goto();
        const vendors = new DataTableRegion(page, kitchenSink.specimen("Data table"));
        await vendors.waitUntilInteractive();

        await expect(vendors.status).toHaveText("Showing 1–3 of 5 vendors. Sorted by Vendor, A to Z.");
        expect(await vendors.titles()).toEqual(["Deccan Logistics", "Kaveri Traders", "Konark Electricals"]);
        await vendors.sortBy("Outstanding", "Outstanding, highest first");
        await expect(vendors.status).toContainText("Sorted by Outstanding, highest first.");
        expect(await vendors.titles()).toEqual(["Deccan Logistics", "Nilgiri Print House", "Kaveri Traders"]);

        await vendors.nextPage.click();
        await expect(vendors.pageLabel).toHaveText("Page 2 of 2");
        await expect(vendors.status).toContainText("Showing 4–5 of 5 vendors.");
        expect(await vendors.titles()).toEqual(["Konark Electricals", "Thar Solar Systems"]);
        await expect(vendors.nextPage).toHaveAttribute("aria-disabled", "true");

        await vendors.container
          .getByRole("checkbox", { name: "Select Thar Solar Systems" })
          .filter({ visible: true })
          .click();
        await expect(vendors.selection).toHaveText("1 vendor selected");
        await vendors.setColumnVisible("City", false);
        if (await vendors.showsCards())
          await expect(vendors.cards.first().getByRole("term")).toHaveText(["Outstanding"]);
        else await expect(vendors.columnHeader("City")).toHaveCount(0);
        await capture("composites-data-table-operated", kitchenSink.specimen("Data table"));

        const withoutRows = new DataTableRegion(page, kitchenSink.specimen("Data table without rows"));
        await expect(withoutRows.container).toContainText("No vendors owe anything.");
        await expect(withoutRows.status).toHaveText("No vendors to show.");
        await expect(
          kitchenSink.specimen("Data table while loading").getByTestId("data-table-skeleton"),
        ).toBeVisible();
        expect(await hasHorizontalOverflow(page), "horizontal overflow").toBe(false);
      },
    );

    forEachTheme(
      "the field frame, error summary and submit button with the keyboard",
      async ({ page, capture }) => {
        const kitchenSink = new KitchenSinkPage(page);
        await kitchenSink.goto();
        const forms = kitchenSink.section("forms");

        const email = forms.getByRole("textbox", { name: "Email for remittance advice" });
        await expect(email).toHaveAttribute("aria-invalid", "true");
        await email.fill("accounts@acme.in");
        await expect(email).not.toHaveAttribute("aria-invalid");
        await expect(email).not.toHaveAttribute("aria-describedby");
        const legalName = forms.getByRole("textbox", { name: "Legal name" });
        await expect(legalName).toHaveAccessibleName("Legal name");
        await expect(legalName).toHaveAttribute("aria-required", "true");
        await expect(
          fieldOf(forms, page.getByRole("textbox", { name: "Legal name" })).getByText("(required)", {
            exact: true,
          }),
        ).toBeVisible();
        await expect(legalName).toHaveAccessibleDescription("As printed on the PAN card.");
        await legalName.focus();
        await page.keyboard.type("Acme Private Limited");
        await expect(legalName).toHaveValue("Acme Private Limited");
        const reminders = forms.getByRole("switch", { name: "Payment reminders" });
        await expect(reminders).toHaveAccessibleDescription("Remind the client 3 days before the due date.");
        await reminders.focus();
        await page.keyboard.press("Space");
        await expect(reminders).not.toBeChecked();
        const creditDays = forms.getByRole("textbox", { name: "Credit days" });
        await creditDays.selectText();
        await page.keyboard.type("45");
        await expect(creditDays).toHaveValue("45");
        await capture("forms-field-frame-operated", kitchenSink.specimen("Field frame"));

        const summary = kitchenSink.specimen("Error summary");
        await tabOntoLink(page, summary.getByRole("link", { name: "Enter the supplier's legal name." }));
        await page.keyboard.press("Enter");
        await expect(legalName).toBeFocused();
        await summary.getByRole("link", { name: "Enter an email address such as accounts@acme.in." }).click();
        await expect(email).toBeFocused();

        const submit = kitchenSink.specimen("Submit button");
        const save = submit.getByRole("button", { name: "Save", exact: true });
        const finish = submit.getByRole("button", { name: "Finish saving" });
        await expect(finish).toBeDisabled();
        await save.focus();
        await page.keyboard.press("Enter");
        await expectPendingSave(save);
        await capture("forms-submit-pending", submit);
        await page.keyboard.press("Space");
        await expectPendingSave(save);
        await page.keyboard.press("Tab");
        await expect(finish).toBeFocused();
        await page.keyboard.press("Space");
        await expect(save).not.toHaveAttribute("aria-busy");
        await expect(save).not.toHaveAttribute("aria-disabled");
        await expect(save).toBeEnabled();
        await expect(finish).toBeDisabled();
        await save.focus();
        await page.keyboard.press("Space");
        await expectPendingSave(save);
        await finish.click();
        await expect(save).not.toHaveAttribute("aria-busy");
      },
    );

    forEachTheme(
      "the amount and identifier inputs with the keyboard and the pointer",
      async ({ page, capture }, isMobile) => {
        const kitchenSink = new KitchenSinkPage(page);
        await kitchenSink.goto();
        const forms = kitchenSink.section("forms");
        const amountValue = forms.getByTestId("forms-amount-value");

        const invoiceAmount = forms.getByRole("textbox", { name: "Invoice amount" });
        await expect(invoiceAmount).toHaveValue("1,23,45,678.50");
        await invoiceAmount.scrollIntoViewIfNeeded();
        const afterFirstFive = await textOffsetPoint(invoiceAmount, "1,23,45");
        if (isMobile) await page.touchscreen.tap(afterFirstFive.x, afterFirstFive.y);
        else await page.mouse.click(afterFirstFive.x, afterFirstFive.y);
        await expect(invoiceAmount).toBeFocused();
        await expect(invoiceAmount).toHaveValue("12345678.5");
        expect(await selection(invoiceAmount), "caret after the digit pointed at").toEqual({
          start: 5,
          end: 5,
        });
        await page.keyboard.type("4");
        await expect(invoiceAmount).toHaveValue("123454678.5");
        await expect(amountValue).toHaveText("Value: 123454678.5");
        await invoiceAmount.blur();
        await expect(invoiceAmount).toHaveValue("12,34,54,678.50");
        if (!isMobile) {
          const start = await textOffsetPoint(invoiceAmount, "12");
          const end = await textOffsetPoint(invoiceAmount, "12,34,54");
          await page.mouse.move(start.x, start.y);
          await page.mouse.down();
          await page.mouse.move(end.x, end.y, { steps: 5 });
          await page.mouse.up();
          await expect(invoiceAmount).toHaveValue("123454678.5");
          expect(await selection(invoiceAmount), "digits dragged over").toEqual({ start: 2, end: 6 });
          await page.keyboard.type("0");
          await expect(invoiceAmount).toHaveValue("120678.5");
          await expect(amountValue).toHaveText("Value: 120678.5");

          await invoiceAmount.blur();
          await expect(invoiceAmount).toHaveValue("1,20,678.50");
          const afterThirdDigit = await textOffsetPoint(invoiceAmount, "1,20");
          await page.mouse.move(afterThirdDigit.x, afterThirdDigit.y);
          await page.mouse.down();
          await expect(invoiceAmount).toBeFocused();
          await expect(invoiceAmount).toHaveValue("1,20,678.50");
          await page.keyboard.press("Tab");
          await expect(invoiceAmount).not.toBeFocused();
          await page.keyboard.press("Shift+Tab");
          await expect(invoiceAmount).toBeFocused();
          await expect(invoiceAmount).toHaveValue("120678.5");
          expect(await selection(invoiceAmount), "selection after tabbing back during the press").toEqual({
            start: 0,
            end: 8,
          });
          await page.mouse.up();
          await expect(invoiceAmount).toHaveValue("120678.5");

          await invoiceAmount.blur();
          await expect(invoiceAmount).toHaveValue("1,20,678.50");
          await page.mouse.down();
          await expect(invoiceAmount).toBeFocused();
          await expect(invoiceAmount).toHaveValue("1,20,678.50");
          await invoiceAmount.dispatchEvent("contextmenu");
          await expect(invoiceAmount).toHaveValue("120678.5");
          expect(await selection(invoiceAmount), "caret after a press a context menu ended").toEqual({
            start: 3,
            end: 3,
          });
          await page.mouse.up();
          await expect(invoiceAmount).toHaveValue("120678.5");
        }
        await invoiceAmount.fill("-1500");
        await expect(invoiceAmount).toHaveValue("-1500");
        await expect(amountValue).toHaveText("Value: -1500");
        await invoiceAmount.fill("1234567890123456");
        await expect(invoiceAmount).toHaveValue("1234567890123456");
        await expect(amountValue).toHaveText("Value: 1234567890123456");
        await invoiceAmount.fill("₹ 1,23,45,678.9");
        await expect(invoiceAmount).toHaveValue("12345678.9");
        await expect(amountValue).toHaveText("Value: 12345678.9");
        await invoiceAmount.blur();
        await expect(invoiceAmount).toHaveValue("1,23,45,678.90");

        const openingBalance = forms.getByRole("textbox", { name: "Opening balance" });
        const adjustment = forms.getByRole("textbox", { name: "Adjustment" });
        await openingBalance.focus();
        await page.keyboard.press("Tab");
        await expect(adjustment).toBeFocused();
        await expect(adjustment).toHaveValue("-2500");
        expect(await selection(adjustment), "selection after tabbing in").toEqual({ start: 0, end: 5 });
        await page.keyboard.type("-1500.5");
        await expect(adjustment).toHaveValue("-1500.5");
        for (let step = 0; step < 5; step += 1) await page.keyboard.press("ArrowLeft");
        await page.keyboard.type("2");
        await expect(adjustment).toHaveValue("-12500.5");
        expect(await selection(adjustment), "caret after the inserted digit").toEqual({ start: 3, end: 3 });
        await page.keyboard.press("Tab");
        await expect(adjustment).not.toBeFocused();
        await expect(adjustment).toHaveValue("-12,500.50");
        await adjustment.fill(`${minusSign}2,500.00`);
        await adjustment.blur();
        await expect(adjustment).toHaveValue("-2,500.00");
        await adjustment.fill("(1,500.50)");
        await adjustment.blur();
        await expect(adjustment).toHaveValue("-1,500.50");
        const discount = forms.getByRole("textbox", { name: "Discount" });
        await expect(discount).toHaveAccessibleDescription("Enter an amount of at most ₹1,00,000.00.");
        await discount.fill("99999.99");
        await expect(discount).not.toHaveAttribute("aria-invalid");
        const rounded = forms.getByRole("textbox", { name: "Rounded total" });
        await rounded.fill("₹25,00,000.00");
        await expect(rounded).toHaveValue("2500000.");
        await rounded.blur();
        await expect(rounded).toHaveValue("25,00,000");

        const gstin = forms.getByRole("textbox", { name: "GSTIN", exact: true });
        await gstin.focus();
        await page.keyboard.type("27 aaacd 1234e 1z5 99");
        await expect(gstin).toHaveValue("27AAACD1234E1Z5");
        await expect(forms.getByTestId("forms-gstin-value")).toHaveText("Value: 27AAACD1234E1Z5");
        await expect(gstin).not.toHaveAttribute("aria-invalid");
        const pan = forms.getByRole("textbox", { name: "PAN", exact: true });
        await pan.selectText();
        await page.keyboard.type("aaacd-1234");
        await expect(pan).toHaveValue("AAACD1234");
        await expect(pan).toHaveAccessibleDescription(
          "Enter a 10-character PAN: five letters, four digits, then a letter.",
        );
        const ifsc = forms.getByRole("textbox", { name: "IFSC" });
        await ifsc.selectText();
        await page.keyboard.type("hdfc0001234");
        await expect(ifsc).toHaveValue("HDFC0001234");
        await expect(ifsc).not.toHaveAttribute("aria-invalid");
        await capture("forms-inputs-operated", kitchenSink.specimen("GSTIN, PAN and IFSC"));
        expect(await hasHorizontalOverflow(page), "horizontal overflow").toBe(false);
      },
    );

    forEachTheme("the date, date-range and combobox inputs and the calendar", async ({ page, capture }) => {
      const kitchenSink = new KitchenSinkPage(page);
      await kitchenSink.goto();
      const forms = kitchenSink.section("forms");

      const dates = kitchenSink.specimen("Date");
      const dueDate = dates.getByRole("textbox", { name: "Due date" });
      await dueDate.fill("1/4/2026");
      await expect(dueDate).toHaveValue("1/4/2026");
      await dueDate.blur();
      await expect(dueDate).toHaveValue("01-04-2026");
      const delivery = dates.getByRole("textbox", { name: "Delivery date" });
      await delivery.fill("28.02.2026");
      await delivery.blur();
      await expect(delivery).toHaveValue("28-02-2026");
      await expect(delivery).not.toHaveAttribute("aria-invalid");

      const invoiceDate = dates.getByRole("textbox", { name: "Invoice date" });
      // While its calendar is open the rest of the page is hidden from assistive technology, so the trigger is found by
      // the id of its field's input and its label attribute rather than by role.
      const invoiceCalendarButton = page.locator(
        "[data-slot='field']:has(#ks-forms-invoice-date) button[aria-label='Choose date']",
      );
      const calendar = page.getByRole("dialog", { name: "Choose date", exact: true });
      await invoiceCalendarButton.focus();
      await page.keyboard.press("Enter");
      await expect(calendar).toBeVisible();
      await expect(invoiceCalendarButton).toHaveAttribute("aria-expanded", "true");
      await expect(calendar.getByRole("button", { name: /\b31 March 2026/ })).toBeFocused();
      await expect(calendar.locator("[data-disabled]")).not.toHaveCount(0);
      await expect(calendar).toMatchAriaSnapshot(`
        - dialog "Choose date":
          - navigation "Navigation bar":
            - button "Go to the Previous Month"
            - button "Go to the Next Month" [disabled]
          - combobox "Choose the Month"
          - combobox "Choose the Year"
          - status: March 2026
          - grid "March 2026":
            - rowgroup:
              - row /31 March 2026, selected/:
                - gridcell "Tuesday, 31 March 2026, selected" [selected]:
                  - button "Tuesday, 31 March 2026, selected": "31"
      `);
      await capture("forms-date-calendar", calendar);
      await page.keyboard.press("ArrowLeft");
      await expect(calendar.getByRole("button", { name: /\b30 March 2026/ })).toBeFocused();
      await page.keyboard.press("Enter");
      await expect(calendar).toBeHidden();
      await expect(invoiceDate).toHaveValue("30-03-2026");
      await expect(forms.getByTestId("forms-date-value")).toHaveText("Value: 2026-03-30");
      await expect(invoiceCalendarButton).toBeFocused();
      await page.keyboard.press("Enter");
      await expect(calendar).toBeVisible();
      await page.keyboard.press("Escape");
      await expect(calendar).toBeHidden();
      await expect(invoiceCalendarButton).toBeFocused();

      const ranges = kitchenSink.specimen("Date range");
      const rangeCalendar = page.getByRole("dialog", { name: "Choose dates", exact: true });
      await fieldOf(ranges, page.getByRole("textbox", { name: "Statement period From" }))
        .getByRole("button", { name: "Choose dates" })
        .click();
      await expect(rangeCalendar).toBeVisible();
      await capture("forms-date-range-calendar", rangeCalendar);
      const rangeRoot = await rangeCalendar.locator("[data-slot='calendar']").elementHandle();
      const rangeMonth = await rangeCalendar
        .getByRole("combobox", { name: "Choose the Month" })
        .first()
        .elementHandle();
      await rangeCalendar.getByRole("button", { name: /\b10 April 2026/ }).click();
      await expect(rangeCalendar).toBeVisible();
      await expect(rangeCalendar.getByRole("gridcell", { name: /\b10 April 2026/ })).toHaveAttribute(
        "aria-selected",
        "true",
      );
      expect(await rangeRoot.evaluate((element) => element.isConnected), "calendar kept").toBe(true);
      expect(await rangeMonth.evaluate((element) => element.isConnected), "month dropdown kept").toBe(true);
      await rangeRoot.dispose();
      await rangeMonth.dispose();
      await rangeCalendar.getByRole("button", { name: /\b20 April 2026/ }).click();
      await expect(rangeCalendar).toBeHidden();
      await expect(forms.getByTestId("forms-date-range-value")).toHaveText("Value: 2026-04-10 to 2026-04-20");
      await expect(ranges.getByRole("textbox", { name: "Statement period To" })).toHaveValue("20-04-2026");
      const contractFrom = ranges.getByRole("textbox", { name: "Contract period From" });
      const contractTo = ranges.getByRole("textbox", { name: "Contract period To" });
      await expect(contractFrom).not.toHaveAttribute("aria-invalid");
      await expect(contractTo).toHaveAttribute("aria-invalid", "true");
      await contractTo.fill("31-12-2026");
      await expect(contractTo).not.toHaveAttribute("aria-invalid");
      await contractFrom.fill("31-02-2026");
      await contractFrom.blur();
      await expect(contractFrom).toHaveAttribute("aria-invalid", "true");
      await expect(contractFrom).toHaveAccessibleDescription(
        "Enter a real date as day-month-year, for example 31-03-2026.",
      );
      await expect(contractTo).not.toHaveAttribute("aria-invalid");
      await contractFrom.fill("01-04-2026");
      await expect(contractFrom).not.toHaveAttribute("aria-invalid");

      const combos = kitchenSink.specimen("Combobox");
      const account = combos.getByRole("combobox", { name: "Expense account", exact: true });
      const accounts = page.getByRole("listbox", { name: "Expense account", exact: true });
      const accountValue = forms.getByTestId("forms-combobox-value");
      await account.click();
      await expect(accounts).toBeVisible();
      await expect(account).toHaveAttribute("aria-expanded", "true");
      await expect(account).not.toHaveAttribute("aria-activedescendant");
      await capture("forms-combobox-open", accounts);
      await account.pressSequentially("cafe");
      await expect(accounts.getByRole("option")).toHaveText(["Café and pantry"]);
      await account.press("ArrowDown");
      await expect(accounts).toMatchAriaSnapshot(`
        - listbox "Expense account":
          - option "Café and pantry" [selected]
      `);

      const emptyMessageShown = await watchEmptyMessage(page, "No account matches.");
      await account.press("Enter");
      expect(await listboxCount(page), "lists open right after Enter chose").toBe(0);
      await expect(account).toHaveValue("Café and pantry");
      await expect(accountValue).toHaveText("Value: cafe");
      await account.click();
      await accounts.getByRole("option", { name: "Conveyance" }).click();
      expect(await listboxCount(page), "lists open right after a click chose").toBe(0);
      await expect(account).toHaveValue("Conveyance");
      await account.fill("ele");
      await expect(accounts.getByRole("option")).toHaveText(["Electricity"]);
      await account.press("Escape");
      expect(await listboxCount(page), "lists open right after Escape").toBe(0);
      await account.fill("tra");
      await expect(accounts.getByRole("option")).toHaveText(["Travel"]);
      await page.keyboard.press("Tab");
      expect(await listboxCount(page), "lists open right after Tab").toBe(0);
      await expect(account).toHaveValue("Conveyance");
      expect(await emptyMessageShown(), "times the empty message showed while a list closed").toBe(0);

      await account.fill("ca");
      for (const { key, caret } of [
        { key: "ArrowLeft", caret: 1 },
        { key: "Home", caret: 0 },
        { key: "ArrowRight", caret: 1 },
        { key: "End", caret: 2 },
      ]) {
        await account.press("ArrowDown");
        await expect(account).toHaveAttribute("aria-activedescendant", /-option-/);
        await account.press(key);
        await expect(accounts).toBeVisible();
        await expect(account).toHaveAttribute("aria-expanded", "true");
        await expect(account).not.toHaveAttribute("aria-activedescendant");
        await expect(accounts.locator("[aria-selected='true']")).toHaveCount(0);
        expect(await selection(account), `caret after ${key}`).toEqual({ start: caret, end: caret });
      }
      await account.press("Enter");
      await expect(accounts).toBeHidden();
      await expect(accountValue).toHaveText("Value: conveyance");
      await account.press("Escape");
      await expect(account).toHaveValue("Conveyance");

      await account.fill("conv");
      await account.press("ArrowDown");
      await account.press("Enter");
      await expect(account).toHaveValue("Conveyance");
      await account.press("ArrowDown");
      await expect(accounts.getByRole("option", { name: "Conveyance" })).toHaveAttribute(
        "aria-selected",
        "true",
      );
      await account.press("ArrowDown");
      await expect(accounts.getByRole("option", { name: "Electricity" })).toHaveAttribute(
        "aria-selected",
        "true",
      );
      await account.press("Enter");
      await expect(accountValue).toHaveText("Value: electricity");
      await account.fill("zzz");
      await expect(combos.getByRole("status").filter({ hasText: "No account matches." })).toHaveCount(1);
      await expect(account).toHaveAttribute("aria-expanded", "false");
      await account.press("Escape");
      await account.press("Escape");
      await expect(account).toHaveValue("Electricity");
      await fieldOf(combos, page.getByRole("combobox", { name: "Expense account", exact: true }))
        .getByRole("button", { name: "Clear Expense account" })
        .click();
      await expect(account).toHaveValue("");
      await expect(accountValue).toHaveText("Value: (none)");
      await expect(account).toBeFocused();

      const supplier = combos.getByRole("combobox", { name: "Supplier", exact: true });
      const suppliers = page.getByRole("listbox", { name: "Supplier", exact: true });
      const supplierValue = forms.getByTestId("forms-supplier-value");
      await expect(supplier).toHaveValue("Globex Cloud Services");
      await expect(supplierValue).toHaveText("Value: globex");
      await supplier.fill("ini");
      await expect(suppliers.getByRole("option")).toHaveText(["Initech Software"]);
      await supplier.press("Escape");
      await expect(suppliers).toBeHidden();
      await supplier.press("Escape");
      await expect(supplier).toHaveValue("Globex Cloud Services");
      await supplier.fill("umb");
      await expect(suppliers.getByRole("option")).toHaveText(["Umbrella Logistics"]);
      await page.keyboard.press("Tab");
      await expect(supplier).not.toBeFocused();
      await expect(supplier).toHaveValue("Globex Cloud Services");
      await expect(supplierValue).toHaveText("Value: globex");

      const costCentre = combos.getByRole("combobox", { name: "Cost centre" });
      await costCentre.click();
      await page.getByRole("listbox", { name: "Cost centre" }).getByRole("option", { name: "Sales" }).click();
      await expect(costCentre).toHaveValue("Sales");
      await expect(costCentre).not.toHaveAttribute("aria-invalid");
      const client = combos.getByRole("combobox", { name: "Client" });
      await client.click();
      await expect(combos.getByRole("status").filter({ hasText: "Searching…" })).toHaveCount(1);
      await expect(client).toHaveAttribute("aria-expanded", "false");
      await client.press("Escape");

      const calendars = kitchenSink.specimen("Calendar");
      const singleDay = calendars.getByRole("grid").nth(0);
      const dayRange = calendars.getByRole("grid").nth(1);
      await singleDay.getByRole("button", { name: /\b17 September 2026/ }).click();
      await expect(calendars.getByTestId("forms-calendar-day")).toHaveText("Selected: 17-09-2026");
      await dayRange.getByRole("button", { name: /\b21 September 2026/ }).click();
      await dayRange.getByRole("button", { name: /\b25 September 2026/ }).click();
      await expect(calendars.getByTestId("forms-calendar-range")).toHaveText(
        "Range: 21-09-2026 to 25-09-2026",
      );
      await calendars.getByRole("button", { name: "Go to the Next Month" }).first().click();
      await expect(calendars.getByRole("grid", { name: "October 2026" })).toBeVisible();
      await capture("forms-calendar-operated", calendars);
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

  test.describe("on a screen 180 pixels tall", () => {
    test.use({ viewport: { width: narrowestScreen.width, height: 180 } });

    test("keeps the combobox list inside the screen with its active option in view", async ({ page }) => {
      const kitchenSink = new KitchenSinkPage(page);
      await kitchenSink.goto();
      const account = kitchenSink
        .specimen("Combobox")
        .getByRole("combobox", { name: "Expense account", exact: true });
      const accounts = page.getByRole("listbox", { name: "Expense account", exact: true });
      const popup = page.locator("[data-slot='popover-content']").filter({ has: accounts });

      await account.focus();
      for (const { key, option } of [
        { key: "ArrowUp", option: "Travel" },
        { key: "ArrowDown", option: "Advertising" },
        { key: "ArrowUp", option: "Travel" },
      ]) {
        await page.keyboard.press(key);
        await expect(accounts.getByRole("option", { name: option })).toHaveAttribute("aria-selected", "true");
        await expect(accounts.getByRole("option", { name: option })).toBeInViewport({ ratio: 1 });
      }
      await expect(popup).toBeInViewport({ ratio: 1 });
      expect(await hasHorizontalOverflow(page), "horizontal overflow").toBe(false);
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
