import { expect, forEachTheme, test } from "../../../fixtures/test";
import { LoginPage } from "../../../pages/identity/auth/login.page";

const apiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
const failureMessage = "We could not sign you in. Try again, or ask your administrator for access.";
const storeLabelSuffix = " (local-dev)";

test.describe("login page", () => {
  forEachTheme("renders the sign-in card with every control", async ({ page, capture, theme }) => {
    const login = new LoginPage(page);
    await login.goto();

    await expect(page).toHaveTitle(/Sign in · ERP-AI-Pro/);
    await expect(login.wordmark).toBeVisible();
    await expect(login.signInButton).toBeVisible();
    await expect(login.signInButton).toHaveAttribute("href", "/api/auth/login?returnUrl=%2F");
    await expect(page.getByText("Single sign-on through Microsoft Entra ID")).toBeVisible();
    await expect(login.failure).toHaveCount(0);
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

  forEachTheme("explains a failed sign-in above the sign-in link", async ({ page, capture }) => {
    const login = new LoginPage(page);
    await login.goto("?error=sign-in-failed");

    await expect(login.failure).toHaveText(failureMessage);
    await expect(login.signInButton).toHaveAttribute("href", "/api/auth/login?returnUrl=%2F");
    await expect(page.getByRole("main")).toMatchAriaSnapshot(`
      - heading "Sign in to your workspace" [level=1]
      - alert: ${failureMessage}
      - link "Continue with Microsoft"
    `);

    await capture("login-sign-in-failed");
  });

  test("shows no failure for any other error value and never repeats it", async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto(`?error=${encodeURIComponent("Your account is locked")}`);

    await expect(login.failure).toHaveCount(0);
    await expect(page.getByRole("main")).not.toContainText("Your account is locked");
  });

  test("root redirects to the login page", async ({ page }) => {
    await page.goto("/");
    await expect(page).toHaveURL(/\/login$/);
  });

  test.describe("sign-in endpoints through the web origin", () => {
    test("send a local return address to Microsoft sign-in without following it", async ({
      baseURL,
      request,
    }) => {
      const systemInfo = await request.get("/api/platform/system-info");
      expect(systemInfo.status()).toBe(200);
      const { applicationName } = (await systemInfo.json()) as { applicationName?: unknown };
      expect(typeof applicationName).toBe("string");
      test.skip(
        !(applicationName as string).endsWith(storeLabelSuffix),
        "the API behind the web origin does not report the local-dev label of the App Configuration store, so it runs without the store and signs in with throwaway ids that Microsoft sign-in does not know",
      );

      const response = await request.get("/api/auth/login?returnUrl=%2F", { maxRedirects: 0 });

      expect(response.status()).toBe(302);
      const location = response.headers()["location"];
      expect(location).toMatch(/^https:\/\/login\.microsoftonline\.com\//);
      const authorize = new URL(location ?? "");
      expect(authorize.searchParams.get("redirect_uri")).toBe(`${baseURL}/api/auth/signin-oidc`);
      expect(authorize.searchParams.get("response_type")).toBe("code");
      expect(authorize.searchParams.get("code_challenge_method")).toBe("S256");
    });

    test("refuse a return address on another site with the API's own problem", async ({ request }) => {
      const response = await request.get(
        `/api/auth/login?returnUrl=${encodeURIComponent("https://example.com")}`,
        { maxRedirects: 0 },
      );

      expect(response.status()).toBe(400);
      expect(response.headers()["location"]).toBeUndefined();
      expect(response.headers()["content-type"]).toContain("application/problem+json");
      expect(response.headers()["content-security-policy"]).toBe(apiContentSecurityPolicy);
      expect(await response.json()).toMatchObject({
        type: "/problems/request.invalid",
        code: "request.invalid",
        status: 400,
        instance: "/api/auth/login",
        errors: { returnUrl: [expect.any(String)] },
      });
    });

    test("answer an anonymous sign-out with an unauthenticated problem instead of a redirect", async ({
      request,
    }) => {
      const response = await request.post("/api/auth/logout", { maxRedirects: 0 });

      expect(response.status()).toBe(401);
      expect(response.headers()["location"]).toBeUndefined();
      expect(response.headers()["content-type"]).toContain("application/problem+json");
      expect(response.headers()["content-security-policy"]).toBe(apiContentSecurityPolicy);
      expect(await response.json()).toMatchObject({
        type: "/problems/request.unauthenticated",
        code: "request.unauthenticated",
        status: 401,
        instance: "/api/auth/logout",
      });
    });
  });
});
