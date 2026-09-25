import { expect, test } from "../../fixtures/test";

const correlationHeader = "x-correlation-id";

test.describe("api problem details", () => {
  test("a request outside the allowed range answers a validation problem naming the field", async ({
    request,
  }) => {
    const response = await request.get("/api/platform/system-info/startups?take=0", {
      headers: { [correlationHeader]: "e2e-validation-1" },
    });

    expect(response.status()).toBe(400);
    expect(response.headers()["content-type"]).toContain("application/problem+json");
    expect(response.headers()[correlationHeader]).toBe("e2e-validation-1");
    expect(await response.json()).toMatchObject({
      type: "/problems/request.invalid",
      code: "request.invalid",
      status: 400,
      instance: "/api/platform/system-info/startups",
      traceId: "e2e-validation-1",
      errors: { take: [expect.stringContaining("between 1 and 100")] },
    });
  });

  test("a query value that is not a number answers a malformed-request problem", async ({ request }) => {
    const response = await request.get("/api/platform/system-info/startups?take=many");

    expect(response.status()).toBe(400);
    expect(await response.json()).toMatchObject({
      type: "/problems/request.malformed",
      code: "request.malformed",
      status: 400,
    });
  });

  test("an unknown route answers a not-found problem whose trace id is the correlation header", async ({
    request,
  }) => {
    const response = await request.get("/api/platform/does-not-exist");

    expect(response.status()).toBe(404);
    const correlationId = response.headers()[correlationHeader];
    expect(correlationId).toMatch(/^[0-9a-f]{32}$/);
    expect(await response.json()).toMatchObject({
      type: "/problems/resource.not-found",
      code: "resource.not-found",
      instance: "/api/platform/does-not-exist",
      traceId: correlationId,
    });
  });

  test("every api response echoes a well-formed correlation id sent by the caller", async ({ request }) => {
    const response = await request.get("/api/platform/system-info", {
      headers: { [correlationHeader]: "e2e-echo.1" },
    });

    expect(response.status()).toBe(200);
    expect(response.headers()[correlationHeader]).toBe("e2e-echo.1");
  });
});
