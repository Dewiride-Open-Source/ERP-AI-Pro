import { requestToken, requestTokenHeader } from "../../../fixtures/sign-in";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { LoginPage } from "../../../pages/identity/auth/login.page";
import { attachmentsPath } from "../../../pages/platform/attachments/files.page";

const apiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
const failureMessage = "We could not sign you in. Try again, or ask your administrator for access.";
const sessionCookie = "__Host-erp-session";
const entraEndpoint = (path: string) =>
  new RegExp(`^https://login\\.microsoftonline\\.com/[0-9a-f-]{36}/oauth2/v2\\.0/${path}$`);

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
      const response = await request.get("/api/auth/login?returnUrl=%2F", { maxRedirects: 0 });

      expect(response.status()).toBe(302);
      const authorize = new URL(response.headers()["location"] ?? "");
      expect(`${authorize.origin}${authorize.pathname}`).toMatch(entraEndpoint("authorize"));
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

  test.describe("signed in", () => {
    test.use({ persona: "accountant" });

    test("the browser reads the signed-in person from the API", async ({ page, person }) => {
      await new LoginPage(page).goto();

      const me = await page.evaluate(async () => {
        const response = await fetch("/api/auth/me");
        return { status: response.status, body: (await response.json()) as unknown };
      });

      expect(me).toEqual({ status: 200, body: person });
      expect(person?.roles).toEqual(["Erp.User"]);
    });

    test.describe("on an unknown API route", () => {
      test.use({ expectedConsoleError: /the server responded with a status of 404/ });

      test("the browser gets a not-found problem instead of the unauthenticated one", async ({ page }) => {
        await new LoginPage(page).goto();

        const answer = await page.evaluate(async () => {
          const response = await fetch("/api/platform/does-not-exist");
          return {
            status: response.status,
            contentType: response.headers.get("content-type"),
            body: (await response.json()) as unknown,
          };
        });

        expect(answer).toMatchObject({
          status: 404,
          contentType: expect.stringContaining("application/problem+json"),
          body: {
            type: "/problems/resource.not-found",
            code: "resource.not-found",
            status: 404,
            instance: "/api/platform/does-not-exist",
          },
        });
      });
    });

    test("the sign-in sends a signed-in person straight to the return address", async ({ page }) => {
      await page.goto(`/api/auth/login?returnUrl=${encodeURIComponent(attachmentsPath)}`);

      await expect(page).toHaveURL((url) => url.pathname === attachmentsPath);
    });

    test("signing out ends the session at Microsoft and leaves a copy of its cookie refused", async ({
      baseURL,
      context,
      page,
      request,
    }) => {
      const session = (await context.cookies()).find((cookie) => cookie.name === sessionCookie);
      expect(session, "the session cookie of the signed-in person").toBeDefined();

      const signOut = await page.request.post("/api/auth/logout", {
        headers: { [requestTokenHeader]: await requestToken(context) },
        maxRedirects: 0,
      });

      expect(signOut.status()).toBe(302);
      const endSession = new URL(signOut.headers()["location"] ?? "");
      expect(`${endSession.origin}${endSession.pathname}`).toMatch(entraEndpoint("logout"));
      expect(endSession.searchParams.get("post_logout_redirect_uri")).toBe(
        `${baseURL}/api/auth/signout-callback-oidc`,
      );
      expect((await page.request.get("/api/auth/me")).status()).toBe(401);
      const copy = await request.get("/api/auth/me", {
        headers: { cookie: `${sessionCookie}=${session?.value ?? ""}` },
      });
      expect(copy.status()).toBe(401);
    });
  });
});
