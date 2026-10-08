import { requestToken, requestTokenHeader, sessionCookie } from "../../../fixtures/sign-in";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { LoginPage } from "../../../pages/identity/auth/login.page";
import { attachmentsPath } from "../../../pages/platform/attachments/files.page";

const apiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
const failureMessage = "We could not sign you in. Try again, or ask your administrator for access.";
const sessionEndedMessage = "Your session has ended. Sign in again to carry on where you left off.";
const signedOutMessage = "You have signed out.";
const entraEndpoint = (path: string) =>
  new RegExp(`^https://login\\.microsoftonline\\.com/[0-9a-f-]{36}/oauth2/v2\\.0/${path}$`);
const signInHref = (path: string) => `/api/auth/login?returnUrl=${encodeURIComponent(path)}`;

// Every page of the shell, the home page among them, and an address no page answers.
const signedInPages = [
  "/",
  "/platform/system-info",
  "/platform/attachments",
  "/platform/attachments?sort=fileName:asc",
  "/design/kitchen-sink",
  "/design/form-kit",
  "/design/data-table",
  "/design/feedback",
  "/design/feedback/report",
  "/does-not-exist",
];

test.describe("login page", () => {
  test.describe("signed out", () => {
    test.use({ persona: null });

    forEachTheme("renders the sign-in card with every control", async ({ page, capture, theme }) => {
      const login = new LoginPage(page);
      await login.goto();

      await expect(page).toHaveTitle(/Sign in · ERP-AI-Pro/);
      await expect(login.wordmark).toBeVisible();
      await expect(login.signInButton).toBeVisible();
      await expect(login.signInButton).toHaveAttribute("href", "/api/auth/login?returnUrl=%2F");
      await expect(page.getByText("Single sign-on through Microsoft Entra ID")).toBeVisible();
      await expect(login.failure).toHaveCount(0);
      await expect(login.notice).toHaveCount(0);
      if (theme === "dark") {
        await expect(page.locator("html")).toHaveClass(/dark/);
      } else {
        await expect(page.locator("html")).not.toHaveClass(/dark/);
      }

      await expect(page.getByRole("main")).toMatchAriaSnapshot(`
        - heading "Sign in to your workspace" [level=1]
        - link "Continue with Microsoft"
      `);
      if ((page.viewportSize()?.width ?? 0) >= 1024) {
        await expect(login.brand).toBeVisible();
        await expect(login.brand).toMatchAriaSnapshot(`
          - paragraph: Dewiride Technologies
          - paragraph: One workspace for the whole company.
          - list:
            - listitem: /One Microsoft sign-in/
            - listitem: /Sessions that end on their own/
            - listitem: /Files kept private/
        `);
      } else {
        await expect(login.brand).toBeHidden();
      }

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

    forEachTheme(
      "explains that the session ended and signs in again to the page it came from",
      async ({ page, capture }) => {
        const login = new LoginPage(page);
        const returnPath = `${attachmentsPath}?page=2`;
        await login.goto(`?returnUrl=${encodeURIComponent(returnPath)}&reason=session-ended`);

        await expect(login.notice).toHaveText(sessionEndedMessage);
        await expect(login.notice).toHaveRole("status");
        await expect(login.failure).toHaveCount(0);
        await expect(login.signInButton).toHaveAttribute("href", signInHref(returnPath));
        await expect(page.getByRole("main")).toMatchAriaSnapshot(`
          - heading "Sign in to your workspace" [level=1]
          - status: ${sessionEndedMessage}
          - link "Continue with Microsoft"
        `);

        await capture("login-session-ended");
      },
    );

    forEachTheme("confirms a completed sign-out", async ({ page, capture }) => {
      const login = new LoginPage(page);
      await login.goto("?reason=signed-out");

      await expect(login.notice).toHaveText(signedOutMessage);
      await expect(login.notice).toHaveRole("status");
      await expect(login.signInButton).toHaveAttribute("href", "/api/auth/login?returnUrl=%2F");

      await capture("login-signed-out");
    });

    test("shows no failure or notice for any other value and never repeats it", async ({ page }) => {
      const login = new LoginPage(page);
      await login.goto(
        `?error=${encodeURIComponent("Your account is locked")}&reason=${encodeURIComponent("Call 555-0100")}`,
      );

      await expect(login.failure).toHaveCount(0);
      await expect(login.notice).toHaveCount(0);
      await expect(page.getByRole("main")).not.toContainText("Your account is locked");
      await expect(page.getByRole("main")).not.toContainText("555-0100");
    });

    test("drops a return address on another site before the page offers it", async ({ page }) => {
      const login = new LoginPage(page);
      await login.goto(
        `?returnUrl=${encodeURIComponent("https://example.com/platform")}&reason=session-ended`,
      );

      await expect(page).toHaveURL(
        (url) => url.pathname === "/login" && url.search === "?reason=session-ended",
      );
      await expect(login.notice).toHaveText(sessionEndedMessage);
      await expect(login.signInButton).toHaveAttribute("href", "/api/auth/login?returnUrl=%2F");
    });

    test("root redirects to the login page", async ({ page }) => {
      await page.goto("/");
      await expect(page).toHaveURL(/\/login$/);
    });

    // The return address keeps the page's path and query as the web server received them, which may escape characters the
    // test wrote plainly, so the addresses are compared as the browser will read them.
    test("sends every page to the sign-in page with the page as its return address", async ({ request }) => {
      const origin = "https://erp.invalid";
      for (const path of signedInPages) {
        const response = await request.get(path, { maxRedirects: 0 });

        expect(response.status(), path).toBe(307);
        const location = new URL(response.headers()["location"] ?? "", origin);
        expect(location.pathname, path).toBe("/login");
        const returnUrl = location.searchParams.get("returnUrl");
        if (path === "/") {
          expect(returnUrl, path).toBeNull();
          continue;
        }
        const returned = new URL(returnUrl ?? "", origin);
        const page = new URL(path, origin);
        expect(returned.pathname, path).toBe(page.pathname);
        expect([...returned.searchParams], path).toEqual([...page.searchParams]);
      }
    });

    test("a page opened without a session offers a sign-in that comes back to it", async ({ page }) => {
      const login = new LoginPage(page);
      const returnPath = `${attachmentsPath}?page=2`;

      await page.goto(returnPath);

      await expect(login.heading).toBeVisible();
      await expect(page).toHaveURL((url) => url.searchParams.get("returnUrl") === returnPath);
      await expect(login.signInButton).toHaveAttribute("href", signInHref(returnPath));
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
  });

  test.describe("signed in", () => {
    test.use({ persona: "accountant" });

    test("the browser reads the signed-in person from the API", async ({ page, person }) => {
      await page.goto("/");

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
        await page.goto("/");

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

    test("the sign-in page sends a person who is signed in straight on", async ({ page }) => {
      await page.goto("/login");
      await expect(page).toHaveURL((url) => url.pathname === "/");

      await page.goto(`/login?returnUrl=${encodeURIComponent(attachmentsPath)}&reason=session-ended`);
      await expect(page).toHaveURL((url) => url.pathname === attachmentsPath);
    });

    test("the sign-in sends a signed-in person straight to the return address", async ({ page }) => {
      await page.goto(signInHref(attachmentsPath));

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
      expect(endSession.searchParams.get("logout_hint")).toBeTruthy();
      expect((await page.request.get("/api/auth/me")).status()).toBe(401);
      const copy = await request.get("/api/auth/me", {
        headers: { cookie: `${sessionCookie}=${session?.value ?? ""}` },
      });
      expect(copy.status()).toBe(401);
    });
  });
});
