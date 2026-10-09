import { expect, forEachTheme, test } from "../../../fixtures/test";
import { AttachmentsPage, attachmentsPath } from "../../../pages/platform/attachments/files.page";
import { homePath, StartPage } from "../../../pages/platform/home/start.page";
import { SystemInfoPage } from "../../../pages/platform/system-info/info.page";
import { AppShell, showsNavigationDrawer } from "../../../pages/shared/layout/app-shell.page";

const systemInfoPath = "/platform/system-info";

const keyboardHint = "↑ ↓ to move, Enter to open, Esc to close.";

const hintMinWidth = 640;

const applePlatform = /Mac|iPhone|iPad|iPod/;

async function expectActive(shell: AppShell, name: string): Promise<void> {
  const option = shell.paletteOption(name);
  await expect(option).toHaveAttribute("aria-selected", "true");
  await expect(shell.paletteResults.getByRole("option", { selected: true })).toHaveCount(1);
  await expect(shell.paletteSearch).toHaveAttribute(
    "aria-activedescendant",
    (await option.getAttribute("id")) ?? "",
  );
}

test.describe("page search", () => {
  forEachTheme(
    "the Search button opens it with every page, and typing narrows the list or says that nothing matches",
    async ({ page, capture }) => {
      const shell = new AppShell(page);
      const hint = shell.palette.getByRole("paragraph").filter({ hasText: "to move," });
      await new StartPage(page).goto();
      await shell.waitUntilInteractive();

      await expect(shell.searchButton).toHaveAccessibleName("Search");
      await expect(shell.searchButton).toHaveAttribute("aria-keyshortcuts", "Control+K Meta+K");
      if (shell.usesDrawer) await expect(shell.searchButton.locator("kbd")).toBeHidden();
      else {
        const userAgent = await page.evaluate(() => navigator.userAgent);
        await expect(shell.searchButton).toContainText("Search");
        await expect(shell.searchButton.locator("kbd")).toHaveText(
          applePlatform.test(userAgent) ? "⌘K" : "Ctrl K",
        );
      }

      await shell.searchButton.click();
      await expect(shell.palette).toBeVisible();
      await expect(shell.paletteSearch).toBeFocused();
      await expect(shell.paletteSearch).toHaveAttribute("placeholder", "Search pages…");
      await expect(shell.palette).toMatchAriaSnapshot(`
        - dialog "Go to a page":
          - heading "Go to a page" [level=2]
          - combobox "Search pages" [expanded]
          - status
          - listbox "Pages":
            - option "Home" [selected]
            - group "Platform":
              - text: Platform
              - option "System"
              - option "Attachments"
      `);
      await expect(shell.paletteResults.getByRole("option")).toHaveCount(3);
      await expectActive(shell, "Home");
      await expect(shell.paletteStatus).toHaveText("");
      await expect(shell.paletteOption("Home")).not.toHaveAttribute("aria-describedby");
      await expect(shell.paletteOption("System")).toHaveAccessibleDescription(
        "See which version of the ERP is running, since when, and its recent starts.",
      );
      await expect(shell.paletteOption("Attachments")).toHaveAccessibleDescription(
        "Upload, download and delete the files kept with the ERP.",
      );
      await expect(shell.palette).toHaveAccessibleDescription(keyboardHint);
      if ((page.viewportSize()?.width ?? 0) >= hintMinWidth) await expect(hint).toBeVisible();
      else await expect(hint).toBeHidden();
      await capture("palette-open", shell.palette);

      await shell.paletteSearch.fill("plat");
      await expect(shell.paletteStatus).toHaveText("2 pages");
      await expect(shell.paletteResults).toMatchAriaSnapshot(`
        - listbox "Pages":
          - group "Platform":
            - text: Platform
            - option "System" [selected]
            - option "Attachments"
      `);
      await expect(shell.paletteResults.getByRole("option")).toHaveCount(2);
      await shell.paletteSearch.fill("plat att");
      await expect(shell.paletteStatus).toHaveText("1 page");
      await expect(shell.paletteResults.getByRole("option")).toHaveCount(1);
      await expectActive(shell, "Attachments");
      await capture("palette-filtered", shell.palette);
      await shell.paletteSearch.fill("recent starts");
      await expect(shell.paletteStatus).toHaveText("1 page");
      await expect(shell.paletteResults.getByRole("option")).toHaveCount(1);
      await expectActive(shell, "System");

      await shell.paletteSearch.fill("zzz");
      await expect(shell.paletteResults).toHaveCount(0);
      await expect(shell.paletteEmpty).toBeVisible();
      await expect(shell.paletteStatus).toHaveText("No page matches your search.");
      await expect(shell.paletteSearch).toHaveAttribute("aria-expanded", "false");
      await expect(shell.paletteSearch).not.toHaveAttribute("aria-activedescendant");
      await expect(shell.paletteSearch).not.toHaveAttribute("aria-controls");
      await capture("palette-no-match", shell.palette);
      await page.keyboard.press("Enter");
      await expect(shell.palette).toBeVisible();
      await expect(page).toHaveURL((url) => url.pathname === homePath);

      await shell.paletteSearch.fill("");
      await expect(shell.paletteResults.getByRole("option")).toHaveCount(3);
      await expect(shell.paletteStatus).toHaveText("");
      await expect(shell.paletteSearch).toHaveAttribute("aria-expanded", "true");
      await expectActive(shell, "Home");

      await shell.paletteOption("Attachments").click();
      await expect(page).toHaveURL((url) => url.pathname === attachmentsPath);
      await expect(shell.palette).toBeHidden();
      await expect(new AttachmentsPage(page).heading).toBeVisible();
      await expect(shell.searchButton).toBeFocused();
    },
  );

  test("the arrow keys move through the pages, wrapping at both ends, and Enter opens the active one", async ({
    page,
  }) => {
    const shell = new AppShell(page);
    await new StartPage(page).goto();
    await shell.waitUntilInteractive();
    await shell.searchButton.click();
    await expect(shell.paletteSearch).toBeFocused();
    await expectActive(shell, "Home");

    for (const [key, active] of [
      ["ArrowDown", "System"],
      ["ArrowDown", "Attachments"],
      ["ArrowDown", "Home"],
      ["ArrowUp", "Attachments"],
      ["ArrowUp", "System"],
    ] as const) {
      await page.keyboard.press(key);
      await expectActive(shell, active);
    }
    await expect(shell.paletteSearch).toBeFocused();
    await page.keyboard.press("Enter");

    await expect(page).toHaveURL((url) => url.pathname === systemInfoPath);
    await expect(shell.palette).toBeHidden();
    await expect(new SystemInfoPage(page).heading).toBeVisible();
    await expect(shell.searchButton).toBeFocused();
  });

  test("pointing at a page makes it the active one, and a click opens it", async ({ page, hasTouch }) => {
    test.skip(hasTouch, "a touch screen has no pointer that rests on a page without choosing it");
    const shell = new AppShell(page);
    await new StartPage(page).goto();
    await shell.waitUntilInteractive();
    await shell.searchButton.click();
    await expectActive(shell, "Home");

    await shell.paletteOption("Attachments").hover();
    await expectActive(shell, "Attachments");
    await shell.paletteOption("System").hover();
    await expectActive(shell, "System");
    await shell.paletteOption("Attachments").click();

    await expect(page).toHaveURL((url) => url.pathname === attachmentsPath);
    await expect(shell.palette).toBeHidden();
    await expect(new AttachmentsPage(page).heading).toBeVisible();
    await expect(shell.searchButton).toBeFocused();
  });

  test("Escape closes it and gives focus back to the Search button", async ({ page }) => {
    const shell = new AppShell(page);
    await new StartPage(page).goto();
    await shell.waitUntilInteractive();
    await shell.searchButton.click();
    await expect(shell.palette).toBeVisible();

    await page.keyboard.press("Escape");
    await expect(shell.palette).toBeHidden();
    await expect(shell.searchButton).toBeFocused();
  });

  test.describe("with a keyboard", () => {
    test.skip(
      ({ viewport }) => showsNavigationDrawer(viewport),
      "a phone opens the page search from the Search button",
    );

    test("Ctrl+K opens it from anywhere, closes it again and gives focus back to where it was", async ({
      page,
    }) => {
      const shell = new AppShell(page);
      const start = new StartPage(page);
      await start.goto();
      await shell.waitUntilInteractive();

      await page.keyboard.press("ControlOrMeta+k");
      await expect(shell.palette).toBeVisible();
      await expect(shell.paletteSearch).toBeFocused();
      await page.keyboard.press("ControlOrMeta+k");
      await expect(shell.palette).toBeHidden();
      await expect(shell.searchButton, "the Search button, because the page itself had focus").toBeFocused();

      const openSystem = start.openLink("System");
      await openSystem.focus();
      await page.keyboard.press("ControlOrMeta+k");
      await expect(shell.palette).toBeVisible();
      await page.keyboard.press("ControlOrMeta+k");
      await expect(shell.palette).toBeHidden();
      await expect(openSystem).toBeFocused();
      await page.keyboard.press("ControlOrMeta+k");
      await expect(shell.palette).toBeVisible();
      await page.keyboard.press("Escape");
      await expect(shell.palette).toBeHidden();
      await expect(openSystem).toBeFocused();
    });

    test("a page chosen after Ctrl+K leaves focus on the Search button, whatever had it before", async ({
      page,
    }) => {
      const shell = new AppShell(page);
      const start = new StartPage(page);
      await start.goto();
      await shell.waitUntilInteractive();
      await start.openLink("System").focus();

      await page.keyboard.press("ControlOrMeta+k");
      await expect(shell.palette).toBeVisible();
      await shell.paletteSearch.fill("att");
      await expectActive(shell, "Attachments");
      await page.keyboard.press("Enter");

      await expect(page).toHaveURL((url) => url.pathname === attachmentsPath);
      await expect(shell.palette).toBeHidden();
      await expect(new AttachmentsPage(page).heading).toBeVisible();
      await expect(shell.searchButton).toBeFocused();
    });
  });
});
