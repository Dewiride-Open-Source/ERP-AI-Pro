import { antiforgeryCookie, requestTokenCookie, sessionCookie, signIn } from "../../fixtures/sign-in";
import { expect, test } from "../../fixtures/test";

const signInCallbackPath = "/api/auth/signin-oidc";

const signInCookiePrefixes = ["__Secure-erp-correlation.", "__Secure-erp-nonce."];

const signInWindowSeconds = 15 * 60;

test.describe("web origin security", () => {
  test.describe("without a session", () => {
    test.use({ persona: null });

    test("pages carry a nonce-based content security policy and hardening headers", async ({ page }) => {
      const response = await page.goto("/login");
      expect(response).not.toBeNull();
      const headers = response!.headers();

      const csp = headers["content-security-policy"] ?? "";
      const nonce = /'nonce-([^']+)'/.exec(csp)?.[1];
      expect(nonce, "nonce in the CSP header").toBeTruthy();
      expect(csp).toContain("'strict-dynamic'");
      expect(csp).toContain("frame-ancestors 'none'");
      expect(csp).toContain("object-src 'none'");
      expect(csp).toContain(
        "form-action 'self' https://login.microsoftonline.com/common/oauth2/v2.0/logout;",
      );
      expect(csp).toContain("upgrade-insecure-requests");
      expect(headers["x-content-type-options"]).toBe("nosniff");
      expect(headers["referrer-policy"]).toBe("strict-origin-when-cross-origin");
      expect(headers["permissions-policy"]).toContain("camera=()");
      expect(headers["x-powered-by"]).toBeUndefined();

      const scriptNonces = await page.locator("script").evaluateAll((scripts) =>
        scripts
          .filter((script): script is HTMLScriptElement => script instanceof HTMLScriptElement)
          .filter((script) => script.type !== "application/json" && script.type !== "application/ld+json")
          .map((script) => script.nonce),
      );
      expect(scriptNonces.length).toBeGreaterThan(0);
      for (const value of scriptNonces) expect(value).toBe(nonce);

      const second = await page.goto("/login");
      const secondNonce = /'nonce-([^']+)'/.exec(second?.headers()["content-security-policy"] ?? "")?.[1];
      expect(secondNonce).not.toBe(nonce);
    });

    test("the sign-in start writes only Secure-prefixed correlation and nonce cookies for the sign-in callback", async ({
      context,
    }) => {
      const startedAt = Date.now() / 1000;

      const answer = await context.request.get("/api/auth/login?returnUrl=%2F", { maxRedirects: 0 });

      expect(answer.status()).toBe(302);
      const cookies = await context.cookies();
      expect(cookies.map((cookie) => cookie.name.slice(0, cookie.name.indexOf(".") + 1)).sort()).toEqual(
        signInCookiePrefixes,
      );
      for (const cookie of cookies) {
        expect(cookie, cookie.name).toMatchObject({
          path: signInCallbackPath,
          secure: true,
          httpOnly: true,
          sameSite: "None",
        });
        expect(cookie.expires, `${cookie.name} lasts the sign-in only`).toBeGreaterThan(startedAt);
        expect(cookie.expires, `${cookie.name} lasts the sign-in only`).toBeLessThanOrEqual(
          startedAt + signInWindowSeconds + 60,
        );
      }
    });

    test("crawlers read the rules of robots.txt", async ({ request }) => {
      const response = await request.get("/robots.txt", { maxRedirects: 0 });

      expect(response.status()).toBe(200);
      expect(response.headers()["content-type"]).toContain("text/plain");
      expect(await response.text()).toContain("Disallow: /");
    });
  });

  test("the session and antiforgery cookies are host-only cookies that end with the browser session", async ({
    context,
  }) => {
    const cookies = new Map((await context.cookies()).map((cookie) => [cookie.name, cookie]));

    const sessionOnly = { path: "/", secure: true, expires: -1 };
    expect(cookies.get(sessionCookie)).toMatchObject({ ...sessionOnly, httpOnly: true, sameSite: "Lax" });
    expect(cookies.get(antiforgeryCookie)).toMatchObject({
      ...sessionOnly,
      httpOnly: true,
      sameSite: "Strict",
    });
    expect(cookies.get(requestTokenCookie)).toMatchObject({
      ...sessionOnly,
      httpOnly: false,
      sameSite: "Strict",
    });
  });

  test("API responses served through the web origin keep the API hardening headers", async ({ request }) => {
    await signIn(request, "accountant");
    const info = await request.get("/api/platform/system-info");
    expect(info.status()).toBe(200);
    expect(info.headers()["content-security-policy"]).toBe("default-src 'none'; frame-ancestors 'none'");
    expect(info.headers()["x-content-type-options"]).toBe("nosniff");
    expect(info.headers()["cache-control"]).toBe("no-store");
    expect(info.headers()["cross-origin-resource-policy"]).toBe("same-origin");
    expect(info.headers()["cross-origin-opener-policy"]).toBe("same-origin");
    expect(info.headers()["x-permitted-cross-domain-policies"]).toBe("none");
    expect(info.headers()["strict-transport-security"]).toBeUndefined();
  });
});
