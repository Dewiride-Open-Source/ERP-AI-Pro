import { expect, test } from "../../../fixtures/test";
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
});
