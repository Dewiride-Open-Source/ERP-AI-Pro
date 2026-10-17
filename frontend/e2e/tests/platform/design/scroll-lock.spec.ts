import type { Locator, Page } from "@playwright/test";

import { expect, test } from "../../../fixtures/test";
import { KitchenSinkPage } from "../../../pages/platform/design/kitchen-sink.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

// A scrollbar is a whole number of device pixels, so at a display scale of 110 % its width in CSS pixels has a fraction,
// which a scroll lock's whole-pixel margin misses; Playwright hides scrollbars unless it is told not to.
test.use({
  launchOptions: { args: ["--force-device-scale-factor=1.1"], ignoreDefaultArgs: ["--hide-scrollbars"] },
});

test.skip(
  ({ browserName, isMobile }) => browserName !== "chromium" || isMobile,
  "the display scale and the scrollbar are switches of desktop Chromium",
);

type Place = { readonly pageWidth: number; readonly accountRight: number };

async function rightEdge(locator: Locator): Promise<number> {
  const box = await locator.boundingBox();
  expect(box, "a box on screen").not.toBeNull();
  return (box?.x ?? 0) + (box?.width ?? 0);
}

async function placeOfThePage(page: Page): Promise<Place> {
  return {
    pageWidth: await page.evaluate(() => document.body.getBoundingClientRect().width),
    accountRight: await rightEdge(new AppShell(page).account),
  };
}

function expectSamePlace(place: Place, before: Place, moment: string): void {
  expect(place.pageWidth, `the page's width ${moment}`).toBeCloseTo(before.pageWidth, 1);
  expect(place.accountRight, `the right edge of the account menu in the top bar ${moment}`).toBeCloseTo(
    before.accountRight,
    1,
  );
}

async function expectScrollbarWithAFraction(page: Page): Promise<void> {
  const width = await page.evaluate(() => {
    const viewport = document.createElement("div");
    viewport.style.cssText =
      "position:fixed;inset-inline-start:0;inline-size:100vw;block-size:0;visibility:hidden";
    document.body.append(viewport);
    const viewportWidth = viewport.getBoundingClientRect().width;
    viewport.remove();
    return viewportWidth - document.documentElement.getBoundingClientRect().width;
  });
  expect(width, "the width of the page's scrollbar").toBeGreaterThan(0);
  expect(Math.abs(width - Math.round(width)), `the fraction of a ${width}px scrollbar`).toBeGreaterThan(0.01);
}

test.describe("scroll lock on the design system's harness", () => {
  test("keeps the page in place while each layer that locks its scrolling opens and closes", async ({
    page,
  }) => {
    const kitchenSink = new KitchenSinkPage(page);
    await kitchenSink.goto();
    await expectScrollbarWithAFraction(page);
    const body = page.locator("body");
    const before = await placeOfThePage(page);
    const overlays = kitchenSink.section("overlays");
    const layers = [
      {
        name: "a dialog",
        trigger: overlays.getByRole("button", { name: "Edit contact" }),
        layer: page.getByRole("dialog", { name: "Edit contact" }),
      },
      {
        name: "an alert dialog",
        trigger: overlays.getByRole("button", { name: "Delete draft" }),
        layer: page.getByRole("alertdialog", { name: "Delete this draft invoice?" }),
      },
      {
        name: "a sheet",
        trigger: overlays.getByRole("button", { name: "Open filters" }),
        layer: page.getByRole("dialog", { name: "Filters" }),
      },
      { name: "a modal menu", trigger: kitchenSink.invoiceActions, layer: kitchenSink.invoiceActionsMenu },
      {
        name: "a calendar",
        trigger: page.locator(
          "[data-slot='field']:has(#ks-forms-invoice-date) button[aria-label='Choose date']",
        ),
        layer: page.getByRole("dialog", { name: "Choose date", exact: true }),
      },
    ];

    for (const { name, trigger, layer } of layers) {
      await test.step(name, async () => {
        await trigger.click();
        await expect(layer).toBeVisible();
        await expect(body).toHaveAttribute("data-scroll-locked", "1");
        expectSamePlace(await placeOfThePage(page), before, `while ${name} is open`);

        await page.keyboard.press("Escape");
        await expect(layer).toBeHidden();
        await expect(body).not.toHaveAttribute("data-scroll-locked");
        expectSamePlace(await placeOfThePage(page), before, `after ${name} closed`);
      });
    }
  });

  test("keeps a toast in its place while a dialog opens over it and closes", async ({ page }) => {
    const kitchenSink = new KitchenSinkPage(page);
    await kitchenSink.goto();
    await expectScrollbarWithAFraction(page);
    await kitchenSink.section("feedback").getByRole("button", { name: "Loading", exact: true }).click();
    // An open modal layer hides the rest of the page, the toast included, from assistive technology, so the toast is
    // found by its element rather than by role.
    const toast = page.locator("[data-sonner-toast]").filter({ hasText: "Preparing the export" });
    await expect(toast).toBeVisible();
    const shown = await rightEdge(toast);

    const body = page.locator("body");
    const dialog = page.getByRole("dialog", { name: "Edit contact" });
    await kitchenSink.section("overlays").getByRole("button", { name: "Edit contact" }).click();
    await expect(dialog).toBeVisible();
    await expect(body).toHaveAttribute("data-scroll-locked", "1");
    expect(await rightEdge(toast), "the right edge of the toast while the dialog is open").toBeCloseTo(
      shown,
      1,
    );

    await page.keyboard.press("Escape");
    await expect(dialog).toBeHidden();
    await expect(body).not.toHaveAttribute("data-scroll-locked");
    expect(await rightEdge(toast), "the right edge of the toast after the dialog closed").toBeCloseTo(
      shown,
      1,
    );
  });
});
