import assert from "node:assert/strict";
import { test } from "node:test";

import { antiforgeryRefusalMessage } from "./antiforgery.ts";
import { ApiError, apiErrorMessage, problemFromBody, toApiError } from "./problem-details.ts";

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

test("toApiError_TimeoutProblem_KeepsTheCodeAndTraceId", () => {
  const thrown = {
    responseStatusCode: 504,
    responseHeaders: {},
    message: "Gateway Timeout",
    type: "/problems/request.timeout",
    title: "Gateway Timeout",
    status: 504,
    instance: "/api/platform/system-info/startups",
    code: "request.timeout",
    traceId,
  };

  const error = toApiError(thrown);

  assert.ok(error instanceof ApiError);
  assert.equal(error.status, 504);
  assert.equal(error.problem.code, "request.timeout");
  assert.equal(error.problem.traceId, traceId);
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

test("apiErrorMessage_AntiforgeryRefusal_IsThePlainSentenceInsteadOfTheDetail", () => {
  for (const code of ["antiforgery.token-missing", "antiforgery.token-invalid"]) {
    const error = new ApiError({
      status: 400,
      type: `/problems/${code}`,
      title: "The request could not be confirmed as coming from this site.",
      detail: "A request that changes data with the session cookie must carry the X-XSRF-TOKEN header.",
      code,
      traceId,
    });

    assert.equal(apiErrorMessage(error), antiforgeryRefusalMessage, code);
  }
});

test("apiErrorMessage_OtherProblem_IsItsDetailOrElseTheErrorMessage", () => {
  const refusedType = new ApiError({
    status: 415,
    title: "Unsupported Media Type",
    detail: "The file's content does not match its declared type.",
    code: "attachment.content-mismatch",
  });
  const invalid = new ApiError({ status: 400, detail: "The link is not valid.", code: "request.invalid" });

  assert.equal(apiErrorMessage(refusedType), "The file's content does not match its declared type.");
  assert.equal(apiErrorMessage(invalid), "The link is not valid.");
  assert.equal(
    apiErrorMessage(new ApiError({ status: 401, title: "Sign in to use this API." })),
    "Sign in to use this API.",
  );
  assert.equal(apiErrorMessage(new ApiError({ status: 503 })), "API request failed with status 503");
});

test("problemFromBody_ProblemJsonReadByTheBrowser_KeepsEveryMemberAndTheFieldErrors", () => {
  const body = {
    type: "/problems/request.invalid",
    title: "One or more validation errors occurred.",
    status: 400,
    instance: "/api/platform/attachments",
    code: "request.invalid",
    traceId,
    errors: { link: ["The Link field is required."], ignored: [1] },
  };

  assert.deepEqual(problemFromBody(400, body), {
    status: 400,
    type: "/problems/request.invalid",
    title: "One or more validation errors occurred.",
    instance: "/api/platform/attachments",
    code: "request.invalid",
    traceId,
    fields: { link: ["The Link field is required."] },
  });
});

test("problemFromBody_BodyThatIsNotAProblem_KeepsOnlyTheStatus", () => {
  assert.deepEqual(problemFromBody(502, "Bad Gateway"), { status: 502 });
  assert.deepEqual(problemFromBody(0, undefined), { status: 0 });
});
