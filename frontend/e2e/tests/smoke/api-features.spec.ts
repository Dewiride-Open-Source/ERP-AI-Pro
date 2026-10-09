import { signIn, signInPath } from "../../fixtures/sign-in";
import { gatedApiBaseURL, gatedBaseURL, gatedFeatureFlags } from "../../fixtures/targets";
import { expect, test } from "../../fixtures/test";

const bearerTokensFlag = "Erp.Platform.Identity.BearerTokens";

test.describe("feature flags", () => {
  test("the primary stack reports every module flag enabled and bearer tokens off through the web origin", async ({
    request,
  }) => {
    const anonymous = await request.get("/api/platform/features");
    expect(anonymous.status()).toBe(401);
    expect(await anonymous.json()).toMatchObject({ code: "request.unauthenticated" });

    await signIn(request, "accountant");
    const response = await request.get("/api/platform/features");
    expect(response.status()).toBe(200);
    expect(response.headers()["content-type"]).toContain("application/json");
    const body: unknown = await response.json();
    expect(body).toMatchObject({
      features: expect.arrayContaining([
        ...gatedFeatureFlags.map((name) => ({ name, enabled: true })),
        { name: bearerTokensFlag, enabled: false },
      ]),
    });
    expect(body).not.toMatchObject({
      features: expect.arrayContaining([
        expect.objectContaining({ name: expect.stringMatching(/^Erp\.Modules\./), enabled: false }),
      ]),
    });
  });

  test("the gated stack reports the modules disabled and answers their routes with problem details", async ({
    request,
  }) => {
    test.skip(!gatedBaseURL, "E2E_GATED_BASE_URL is not set and Playwright did not start the gated pair");

    const anonymous = await request.get(`${gatedApiBaseURL}/api/platform/system-info`);
    expect(anonymous.status()).toBe(401);

    const signedIn = await request.post(`${gatedBaseURL}${signInPath}/accountant`);
    expect(signedIn.status(), "the test sign-in on the gated stack").toBe(200);

    const features = await request.get(`${gatedBaseURL}/api/platform/features`);
    expect(features.status()).toBe(200);
    expect(await features.json()).toMatchObject({
      features: expect.arrayContaining(gatedFeatureFlags.map((name) => ({ name, enabled: false }))),
    });

    const gated = await request.get(`${gatedBaseURL}/api/platform/system-info`);
    expect(gated.status()).toBe(404);
    expect(gated.headers()["content-type"]).toContain("application/problem+json");
    expect(await gated.json()).toMatchObject({
      type: "/problems/feature.disabled",
      code: "feature.disabled",
      instance: "/api/platform/system-info",
    });

    const attachments = await request.get(`${gatedBaseURL}/api/platform/attachments`);
    expect(attachments.status()).toBe(404);
    expect(await attachments.json()).toMatchObject({
      code: "feature.disabled",
      instance: "/api/platform/attachments",
    });
  });
});
