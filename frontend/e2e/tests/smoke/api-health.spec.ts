import { expect, test } from "../../fixtures/test";

test.describe("api smoke", () => {
  test("health, system info and problem details are reachable through the web origin", async ({
    request,
  }) => {
    const health = await request.get("/healthz");
    expect(health.status()).toBe(200);
    expect(await health.json()).toEqual({ status: "Healthy" });

    const info = await request.get("/api/platform/system-info");
    expect(info.status()).toBe(200);
    const body = (await info.json()) as { applicationName: string; version: string };
    expect(body.applicationName).toMatch(/^ERP-AI-Pro( \(local-dev\))?$/);
    expect(body.version).toMatch(/\d+\.\d+\.\d+/);

    const missing = await request.get("/api/platform/does-not-exist");
    expect(missing.status()).toBe(404);
    expect(missing.headers()["content-type"]).toContain("application/problem+json");
  });
});
