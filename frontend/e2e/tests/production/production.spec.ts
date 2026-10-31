import { expect, test } from "../../fixtures/test";
import { LoginPage } from "../../pages/identity/auth/login.page";

const entraAuthorize = /^https:\/\/login\.microsoftonline\.com\/[0-9a-f-]{36}\/oauth2\/v2\.0\/authorize$/;
const apiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
const isAddress = (url: URL) => /^\d{1,3}(\.\d{1,3}){3}$/.test(url.hostname);

test.describe("the production ERP", () => {
  test.use({ persona: null });

  test("opens over a certificate this device trusts and sends a visitor to the sign-in page", async ({
    page,
    baseURL,
  }) => {
    await page.goto("/");

    await expect(page).toHaveURL(`${baseURL}/login?returnUrl=%2F`);
    const login = new LoginPage(page);
    await expect(login.heading).toBeVisible();
    await expect(login.signInButton).toHaveAttribute("href", "/api/auth/login?returnUrl=%2F");
  });

  test("answers its pages with the web app's policy and without the proxy's own headers", async ({
    page,
    baseURL,
  }) => {
    const response = await page.goto("/login");
    expect(response).not.toBeNull();
    const headers = response!.headers();

    const csp = headers["content-security-policy"] ?? "";
    expect(csp).toMatch(/'nonce-[^']+'/);
    expect(csp).toContain("'strict-dynamic'");
    expect(csp).toContain("frame-ancestors 'none'");
    expect(headers["x-content-type-options"]).toBe("nosniff");
    expect(headers["server"]).toBeUndefined();
    expect(headers["via"]).toBeUndefined();
    if (isAddress(new URL(baseURL ?? ""))) expect(headers["strict-transport-security"]).toBeUndefined();
  });

  test("refuses the API to a visitor with its own problem", async ({ request }) => {
    const response = await request.get("/api/auth/me", { maxRedirects: 0 });

    expect(response.status()).toBe(401);
    expect(response.headers()["content-type"]).toContain("application/problem+json");
    expect(response.headers()["content-security-policy"]).toBe(apiContentSecurityPolicy);
    expect(response.headers()["cache-control"]).toBe("no-store");
    expect(response.headers()["x-correlation-id"]).toBeTruthy();
    expect(response.headers()["server"]).toBeUndefined();
  });

  test("starts a Microsoft sign-in that comes back to this address", async ({ request, baseURL }) => {
    const response = await request.get("/api/auth/login?returnUrl=%2F", { maxRedirects: 0 });

    expect(response.status()).toBe(302);
    const authorize = new URL(response.headers()["location"] ?? "");
    expect(`${authorize.origin}${authorize.pathname}`).toMatch(entraAuthorize);
    expect(authorize.searchParams.get("redirect_uri")).toBe(`${baseURL}/api/auth/signin-oidc`);
    expect(authorize.searchParams.get("response_type")).toBe("code");
    expect(authorize.searchParams.get("response_mode")).toBe("form_post");
    expect(authorize.searchParams.get("code_challenge_method")).toBe("S256");
  });

  test("sends a plain-HTTP visitor to HTTPS", async ({ request, baseURL }) => {
    const plain = new URL(baseURL ?? "");
    plain.protocol = "http:";

    const response = await request.get(`${plain.origin}/login`, { maxRedirects: 0 });

    expect(response.status()).toBe(308);
    expect(response.headers()["location"]).toBe(`${baseURL}/login`);
  });
});
