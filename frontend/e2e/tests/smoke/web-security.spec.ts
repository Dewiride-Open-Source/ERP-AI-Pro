import { expect, test } from "../../fixtures/test";

test.describe("web origin security", () => {
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

  test("API responses served through the web origin keep the API hardening headers", async ({ request }) => {
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
