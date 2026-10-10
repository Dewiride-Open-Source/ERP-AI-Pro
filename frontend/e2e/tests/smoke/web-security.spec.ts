import type { APIResponse } from "@playwright/test";

import {
  antiforgeryCookie,
  requestTokenCookie,
  sessionCookie,
  signIn,
  signInPath,
} from "../../fixtures/sign-in";
import { expect, test } from "../../fixtures/test";

const signInCallbackPath = "/api/auth/signin-oidc";

const signInCookiePrefixes = ["__Secure-erp-correlation.", "__Secure-erp-nonce."];

const remoteAuthenticationTimeoutSeconds = 15 * 60;

type SetCookie = { readonly name: string; readonly attributes: ReadonlyMap<string, string> };

function setCookiesOf(response: APIResponse): SetCookie[] {
  return response
    .headersArray()
    .filter((header) => header.name.toLowerCase() === "set-cookie")
    .map((header) => {
      const [pair = "", ...attributes] = header.value.split(";").map((part) => part.trim());
      return {
        name: pair.slice(0, pair.indexOf("=")),
        attributes: new Map(
          attributes.map((attribute): [string, string] => {
            const separator = attribute.indexOf("=");
            return separator < 0
              ? [attribute.toLowerCase(), ""]
              : [attribute.slice(0, separator).toLowerCase(), attribute.slice(separator + 1)];
          }),
        ),
      };
    });
}

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
      request,
    }) => {
      const startedAt = Date.now();

      const answer = await request.get("/api/auth/login?returnUrl=%2F", { maxRedirects: 0 });

      expect(answer.status()).toBe(302);
      const cookies = setCookiesOf(answer);
      expect(cookies.map((cookie) => cookie.name.slice(0, cookie.name.indexOf(".") + 1)).sort()).toEqual(
        signInCookiePrefixes,
      );
      for (const { name, attributes } of cookies) {
        expect(attributes.get("path"), name).toBe(signInCallbackPath);
        expect(attributes.has("secure"), `${name} is Secure`).toBe(true);
        expect(attributes.has("httponly"), `${name} is HttpOnly`).toBe(true);
        expect(attributes.get("samesite")?.toLowerCase(), name).toBe("none");
        const expires = Date.parse(attributes.get("expires") ?? "");
        expect(expires, `${name} lasts the sign-in only`).toBeGreaterThan(startedAt);
        expect(expires, `${name} lasts the sign-in only`).toBeLessThanOrEqual(
          startedAt + (remoteAuthenticationTimeoutSeconds + 60) * 1000,
        );
      }
    });

    test("the sign-in writes the session and antiforgery cookies as Secure host-only cookies that end with the browser session", async ({
      request,
    }) => {
      const answer = await request.post(`${signInPath}/accountant`);

      expect(answer.status()).toBe(200);
      const cookies = new Map(setCookiesOf(answer).map((cookie) => [cookie.name, cookie.attributes]));
      expect([...cookies.keys()].sort()).toEqual(
        [antiforgeryCookie, sessionCookie, requestTokenCookie].sort(),
      );
      for (const [name, attributes] of cookies) {
        expect(attributes.get("path"), name).toBe("/");
        expect(attributes.has("secure"), `${name} is Secure`).toBe(true);
        expect(attributes.has("domain"), `${name} is host-only`).toBe(false);
        expect(
          attributes.has("expires") || attributes.has("max-age"),
          `${name} ends with the browser session`,
        ).toBe(false);
      }
      expect(cookies.get(sessionCookie)?.has("httponly")).toBe(true);
      expect(cookies.get(sessionCookie)?.get("samesite")?.toLowerCase()).toBe("lax");
      expect(cookies.get(antiforgeryCookie)?.has("httponly")).toBe(true);
      expect(cookies.get(antiforgeryCookie)?.get("samesite")?.toLowerCase()).toBe("strict");
      expect(cookies.get(requestTokenCookie)?.has("httponly")).toBe(false);
      expect(cookies.get(requestTokenCookie)?.get("samesite")?.toLowerCase()).toBe("strict");
    });

    test("crawlers read the rules of robots.txt", async ({ request }) => {
      const response = await request.get("/robots.txt", { maxRedirects: 0 });

      expect(response.status()).toBe(200);
      expect(response.headers()["content-type"]).toContain("text/plain");
      expect(await response.text()).toContain("Disallow: /");
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
