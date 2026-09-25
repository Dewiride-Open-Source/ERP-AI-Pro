import { gatedApiBaseURL, gatedBaseURL, gatedFeatureFlag } from "../../fixtures/targets";
import { expect, test } from "../../fixtures/test";

test.describe("feature flags", () => {
  test("the primary stack reports every module flag enabled through the web origin", async ({ request }) => {
    const response = await request.get("/api/platform/features");
    expect(response.status()).toBe(200);
    expect(response.headers()["content-type"]).toContain("application/json");
    const body: unknown = await response.json();
    expect(body).toMatchObject({
      features: expect.arrayContaining([{ name: gatedFeatureFlag, enabled: true }]),
    });
    expect(body).not.toMatchObject({
      features: expect.arrayContaining([expect.objectContaining({ enabled: false })]),
    });
  });

  test("the gated stack reports the module disabled and answers its routes with problem details", async ({
    request,
  }) => {
    test.skip(!gatedBaseURL, "E2E_GATED_BASE_URL is not set and Playwright did not start the gated pair");

    const features = await request.get(`${gatedApiBaseURL}/api/platform/features`);
    expect(features.status()).toBe(200);
    expect(await features.json()).toMatchObject({
      features: expect.arrayContaining([{ name: gatedFeatureFlag, enabled: false }]),
    });

    const gated = await request.get(`${gatedApiBaseURL}/api/platform/system-info`);
    expect(gated.status()).toBe(404);
    expect(gated.headers()["content-type"]).toContain("application/problem+json");
    expect(await gated.json()).toMatchObject({
      type: "/problems/feature.disabled",
      code: "feature.disabled",
      instance: "/api/platform/system-info",
    });

    const throughWeb = await request.get(`${gatedBaseURL}/api/platform/system-info`);
    expect(throughWeb.status()).toBe(404);
    expect(throughWeb.headers()["content-type"]).toContain("application/problem+json");
  });
});
