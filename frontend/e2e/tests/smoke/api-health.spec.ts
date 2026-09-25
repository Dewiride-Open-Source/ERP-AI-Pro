import { expect, test } from "../../fixtures/test";

const applicationName = /^ERP-AI-Pro( \(local-dev\))?$/;
const utcTimestamp = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|\+00:00)$/;

test.describe("api smoke", () => {
  test("health, system info and problem details are reachable through the web origin", async ({
    request,
  }) => {
    const health = await request.get("/healthz");
    expect(health.status()).toBe(200);
    expect(await health.json()).toEqual({ status: "Healthy" });

    const info = await request.get("/api/platform/system-info");
    expect(info.status()).toBe(200);
    expect(await info.json()).toMatchObject({
      applicationName: expect.stringMatching(applicationName),
      version: expect.stringMatching(/\d+\.\d+\.\d+/),
    });

    const startups = await request.get("/api/platform/system-info/startups");
    expect(startups.status()).toBe(200);
    expect(startups.headers()["content-type"]).toContain("application/json");
    const recent: unknown = await startups.json();
    expect(recent).toMatchObject({
      startups: expect.arrayContaining([
        expect.objectContaining({
          id: expect.stringMatching(/^[0-9a-f-]{36}$/),
          applicationName: expect.stringMatching(applicationName),
          version: expect.stringMatching(/\d+\.\d+\.\d+/),
          framework: expect.stringMatching(/^\.NET \d+\.\d+/),
          startedAt: expect.stringMatching(utcTimestamp),
        }),
      ]),
    });

    const one = await request.get("/api/platform/system-info/startups?take=1");
    expect(one.status()).toBe(200);
    expect(await one.json()).toMatchObject({
      startups: [expect.objectContaining({ id: expect.any(String) })],
    });

    const missing = await request.get("/api/platform/does-not-exist");
    expect(missing.status()).toBe(404);
    expect(missing.headers()["content-type"]).toContain("application/problem+json");
  });
});
