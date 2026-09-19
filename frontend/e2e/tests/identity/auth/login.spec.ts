import { expect, forEachTheme, test } from "../../../fixtures/test";
import { LoginPage } from "../../../pages/identity/auth/login.page";

test.describe("login page", () => {
  forEachTheme("renders the sign-in card with every control", async ({ page, capture, theme }) => {
    const login = new LoginPage(page);
    await login.goto();

    await expect(page).toHaveTitle(/Sign in · ERP-AI-Pro/);
    await expect(login.wordmark).toBeVisible();
    await expect(login.signInButton).toBeVisible();
    await expect(login.signInButton).toHaveAttribute("href", "/api/auth/login?returnUrl=%2F");
    await expect(page.getByText("Single sign-on through Microsoft Entra ID")).toBeVisible();
    if (theme === "dark") {
      await expect(page.locator("html")).toHaveClass(/dark/);
    } else {
      await expect(page.locator("html")).not.toHaveClass(/dark/);
    }

    await expect(page.getByRole("main")).toMatchAriaSnapshot(`
      - heading "Sign in to your workspace" [level=1]
      - link "Continue with Microsoft"
    `);

    await login.signInButton.hover();
    await capture("login");
  });

  test("root redirects to the login page", async ({ page }) => {
    await page.goto("/");
    await expect(page).toHaveURL(/\/login$/);
  });

  test.describe("sign-in link", () => {
    test.use({
      expectedConsoleError: /the server responded with a status of 404|Refused to apply a stylesheet/,
    });

    test("reaches the API session endpoint through the web origin", async ({ page }) => {
      const login = new LoginPage(page);
      await login.goto();

      const [response] = await Promise.all([
        page.waitForResponse((candidate) => candidate.url().includes("/api/auth/login")),
        login.signInButton.click(),
      ]);

      expect(response.status()).toBe(404);
      expect(response.headers()["content-type"]).toContain("application/problem+json");
      expect(response.headers()["content-security-policy"]).toBe(
        "default-src 'none'; frame-ancestors 'none'",
      );
    });
  });
});
