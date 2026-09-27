import { expect, pressArrowUntilChecked, test } from "../../../fixtures/test";
import { SystemInfoPage } from "../../../pages/platform/system-info/info.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

test.describe("theme toggle", () => {
  test("switches between light, dark and system", async ({ page, capture }) => {
    const shell = new AppShell(page);
    await new SystemInfoPage(page).goto();
    await expect(shell.themeToggle).toBeVisible();

    await shell.theme("dark").click();
    await expect(page.locator("html")).toHaveClass(/dark/);
    await expect(shell.theme("dark")).toHaveAttribute("aria-checked", "true");
    await capture("toggle-dark");

    await shell.theme("light").click();
    await expect(page.locator("html")).not.toHaveClass(/dark/);
    await expect(shell.theme("light")).toHaveAttribute("aria-checked", "true");
    await capture("toggle-light");

    await shell.theme("system").click();
    await expect(shell.theme("system")).toHaveAttribute("aria-checked", "true");
    await expect(page.locator("html")).not.toHaveClass(/dark/);

    await page.reload();
    await expect(shell.theme("system")).toHaveAttribute("aria-checked", "true");
  });

  test("arrow keys move and select the theme", async ({ page, capture }) => {
    const shell = new AppShell(page);
    await new SystemInfoPage(page).goto();
    await shell.theme("light").click();
    await page.keyboard.press("Tab");
    await expect(shell.themeToggle.locator(":focus")).toHaveCount(0);
    await page.keyboard.press("Shift+Tab");
    await expect(shell.theme("light")).toBeFocused();

    await pressArrowUntilChecked(page, "ArrowRight", shell.theme("dark"));
    await expect(shell.theme("dark")).toBeFocused();
    await expect(page.locator("html")).toHaveClass(/dark/);
    await expect(shell.theme("dark")).toHaveCSS("outline-style", "solid");
    await expect(shell.theme("dark")).toHaveCSS("outline-width", "2px");
    await capture("toggle-keyboard-dark", shell.themeToggle);

    await pressArrowUntilChecked(page, "ArrowRight", shell.theme("system"));
    await expect(shell.theme("system")).toBeFocused();
    await expect(page.locator("html")).not.toHaveClass(/dark/);
    await pressArrowUntilChecked(page, "ArrowLeft", shell.theme("dark"));
    await pressArrowUntilChecked(page, "ArrowRight", shell.theme("system"));
    await pressArrowUntilChecked(page, "ArrowUp", shell.theme("dark"));
    await expect(shell.theme("dark")).toBeFocused();
    await expect(page.locator("html")).toHaveClass(/dark/);
    await pressArrowUntilChecked(page, "ArrowUp", shell.theme("light"));
    await expect(shell.theme("light")).toBeFocused();
    await pressArrowUntilChecked(page, "ArrowDown", shell.theme("dark"));
    await expect(shell.theme("dark")).toBeFocused();
    await pressArrowUntilChecked(page, "ArrowDown", shell.theme("system"));
    await expect(shell.theme("system")).toBeFocused();
    await expect(page.locator("html")).not.toHaveClass(/dark/);

    await page.keyboard.press("Tab");
    await expect(shell.themeToggle.locator(":focus")).toHaveCount(0);
    await page.keyboard.press("Shift+Tab");
    await expect(shell.theme("system")).toBeFocused();
  });

  test("outlines the chosen theme when the system forces colours", async ({ page, capture }) => {
    await page.emulateMedia({ forcedColors: "active" });
    const shell = new AppShell(page);
    await new SystemInfoPage(page).goto();
    expect(await page.evaluate(() => matchMedia("(forced-colors: active)").matches), "forced colours").toBe(
      true,
    );

    for (const chosen of ["dark", "light", "system"] as const) {
      await shell.theme(chosen).click();
      await shell.theme(chosen).blur();
      await expect(shell.theme(chosen)).toHaveAttribute("aria-checked", "true");
      await expect(shell.theme(chosen)).toHaveCSS("outline-style", "solid");
      await expect(shell.theme(chosen)).toHaveCSS("outline-width", "2px");
      for (const other of (["light", "dark", "system"] as const).filter((option) => option !== chosen)) {
        await expect(shell.theme(other)).toHaveCSS("outline-style", "none");
      }
    }
    await capture("toggle-forced-colours", shell.themeToggle);
  });
});
