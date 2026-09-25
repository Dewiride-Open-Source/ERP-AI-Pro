import assert from "node:assert/strict";
import { test } from "node:test";

import { ApiError, toApiError } from "./problem-details.ts";

const traceId = "0af7651916cd43dd8448eb211c80319c";

test("toApiError_ProblemThrownByTheClient_KeepsEveryProblemMember", () => {
  const thrown = {
    responseStatusCode: 404,
    responseHeaders: {},
    message: "Not Found",
    type: "/problems/resource.not-found",
    title: "Not Found",
    status: 404,
    detail: "No such invoice.",
    instance: "/api/finance/sales/invoices/1",
    code: "resource.not-found",
    traceId,
  };

  const error = toApiError(thrown);

  assert.ok(error instanceof ApiError);
  assert.equal(error.status, 404);
  assert.equal(error.message, "Not Found");
  assert.deepEqual(error.problem, {
    status: 404,
    type: "/problems/resource.not-found",
    title: "Not Found",
    detail: "No such invoice.",
    instance: "/api/finance/sales/invoices/1",
    code: "resource.not-found",
    traceId,
  });
});

test("toApiError_ValidationProblem_ExposesTheFieldErrors", () => {
  const thrown = {
    responseStatusCode: 400,
    title: "One or more validation errors occurred.",
    code: "request.invalid",
    errors: { additionalData: { take: ["The field Take must be between 1 and 100."] } },
  };

  const error = toApiError(thrown);

  assert.ok(error instanceof ApiError);
  assert.equal(error.status, 400);
  assert.deepEqual(error.problem.fields, { take: ["The field Take must be between 1 and 100."] });
});

test("toApiError_FieldErrorsThatAreNotStringLists_AreLeftOut", () => {
  const thrown = {
    responseStatusCode: 400,
    errors: { additionalData: { take: "not a list", page: [1, 2] } },
  };

  const error = toApiError(thrown);

  assert.ok(error instanceof ApiError);
  assert.equal(error.problem.fields, undefined);
});

test("toApiError_StatusWithoutAProblemBody_UsesTheClientMessageAsTheTitle", () => {
  const thrown = Object.assign(new Error("the server returned an unexpected status code 502"), {
    responseStatusCode: 502,
  });

  const error = toApiError(thrown);

  assert.ok(error instanceof ApiError);
  assert.equal(error.status, 502);
  assert.equal(error.problem.title, "the server returned an unexpected status code 502");
  assert.equal(error.problem.code, undefined);
});

test("toApiError_NetworkFailure_IsReturnedUnchanged", () => {
  const failure = new TypeError("fetch failed");

  assert.equal(toApiError(failure), failure);
});

test("toApiError_ValueThatIsNotAnHttpFailure_IsReturnedUnchanged", () => {
  assert.equal(toApiError("boom"), "boom");
  assert.equal(toApiError(null), null);
  const withTextStatus = { responseStatusCode: "404" };
  assert.equal(toApiError(withTextStatus), withTextStatus);
});

test("ApiError_WithoutATitle_DescribesTheStatus", () => {
  assert.equal(new ApiError({ status: 503 }).message, "API request failed with status 503");
});
