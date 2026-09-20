import { gatedApiBaseURL, gatedBaseURL, gatedFeatureFlag } from "../../fixtures/targets";
import { expect, test } from "../../fixtures/test";

type FeaturesResponse = { features: { name: string; enabled: boolean }[] };

test.describe("feature flags", () => {
  test("the primary stack reports every module flag enabled through the web origin", async ({ request }) => {
    const response = await request.get("/api/platform/features");
    expect(response.status()).toBe(200);
    expect(response.headers()["content-type"]).toContain("application/json");
    const body = (await response.json()) as FeaturesResponse;
    expect(body.features).toContainEqual({ name: gatedFeatureFlag, enabled: true });
    expect(body.features.every((feature) => feature.enabled)).toBe(true);
  });

  test("the gated stack reports the module disabled and answers its routes with problem details", async ({
    request,
  }) => {
    test.skip(!gatedBaseURL, "E2E_GATED_BASE_URL is not set and Playwright did not start the gated pair");

    const features = await request.get(`${gatedApiBaseURL}/api/platform/features`);
    expect(features.status()).toBe(200);
    const body = (await features.json()) as FeaturesResponse;
    expect(body.features).toContainEqual({ name: gatedFeatureFlag, enabled: false });

    const gated = await request.get(`${gatedApiBaseURL}/api/platform/system-info`);
    expect(gated.status()).toBe(404);
    expect(gated.headers()["content-type"]).toContain("application/problem+json");
    expect(((await gated.json()) as { code: string }).code).toBe("feature.disabled");

    const throughWeb = await request.get(`${gatedBaseURL}/api/platform/system-info`);
    expect(throughWeb.status()).toBe(404);
    expect(throughWeb.headers()["content-type"]).toContain("application/problem+json");
  });
});
