import { expect, test } from "../../fixtures/test";

type RecentStartups = {
  startups: { id: string; applicationName: string; version: string; framework: string; startedAt: string }[];
};

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

    const startups = await request.get("/api/platform/system-info/startups");
    expect(startups.status()).toBe(200);
    expect(startups.headers()["content-type"]).toContain("application/json");
    const recent = (await startups.json()) as RecentStartups;
    expect(recent.startups.length).toBeGreaterThanOrEqual(1);
    const [latest] = recent.startups;
    expect(latest?.id).toMatch(/^[0-9a-f-]{36}$/);
    expect(latest?.applicationName).toMatch(/^ERP-AI-Pro( \(local-dev\))?$/);
    expect(latest?.version).toMatch(/\d+\.\d+\.\d+/);
    expect(latest?.framework).toMatch(/^\.NET \d+\.\d+/);
    expect(latest?.startedAt).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|\+00:00)$/);
    expect(new Date(latest?.startedAt ?? "").getTime()).not.toBeNaN();

    const missing = await request.get("/api/platform/does-not-exist");
    expect(missing.status()).toBe(404);
    expect(missing.headers()["content-type"]).toContain("application/problem+json");
  });
});
