import assert from "node:assert/strict";
import { test } from "node:test";

import { ApiError, toApiError, type Problem } from "../../api/problem-details.ts";
import { formMessages } from "./form-messages.ts";
import { problemToFormState } from "./problem-to-form-state.ts";

const traceId = "0af7651916cd43dd8448eb211c80319c";

const apiError = (problem: Problem) => new ApiError(problem);

test("problemToFormState_ValidationProblemWithFields_IsInvalidWithFormPaths", () => {
  const error = apiError({
    status: 400,
    code: "request.invalid",
    detail: "CreateOrderRequest is invalid.",
    traceId,
    fields: {
      "customer.shippingAddress.street": ["Enter the street."],
      "orderItems[0].description": ["Enter a description.", "Use 200 characters or fewer."],
      "": ["The order is empty."],
    },
  });

  assert.deepEqual(problemToFormState(error), {
    status: "invalid",
    fieldErrors: {
      "customer.shippingAddress.street": ["Enter the street."],
      "orderItems.0.description": ["Enter a description.", "Use 200 characters or fewer."],
    },
    formErrors: ["The order is empty."],
  });
});

test("problemToFormState_ValidationProblemThrownByTheGeneratedClient_IsInvalid", () => {
  const error = toApiError({
    responseStatusCode: 400,
    title: "One or more validation errors occurred.",
    code: "request.invalid",
    errors: { additionalData: { take: ["The field Take must be between 1 and 100."] } },
  });

  assert.deepEqual(problemToFormState(error), {
    status: "invalid",
    fieldErrors: { take: ["The field Take must be between 1 and 100."] },
    formErrors: [],
  });
});

test("problemToFormState_ValidationProblem_NeverShowsTheDetailOrTitle", () => {
  const error = apiError({
    status: 400,
    title: "One or more validation errors occurred.",
    detail: "CreateOrderRequest is invalid.",
    code: "request.invalid",
    fields: { legalName: ["Enter the legal name."] },
  });

  const shown = JSON.stringify(problemToFormState(error));

  assert.ok(!shown.includes("CreateOrderRequest"));
  assert.ok(!shown.includes("validation errors occurred"));
});

test("problemToFormState_KeyWithoutAFieldPath_IsAFormError", () => {
  const error = apiError({
    status: 400,
    fields: { "lines[x]": ["The lines are unreadable."], type: ["Choose a type."] },
  });

  assert.deepEqual(problemToFormState(error), {
    status: "invalid",
    fieldErrors: {},
    formErrors: ["The lines are unreadable.", "Choose a type."],
  });
});

test("problemToFormState_Aliases_MapApiKeysToTheFormsFields", () => {
  const error = apiError({
    status: 400,
    fields: {
      "bankAccount.Ifsc": ["Enter a valid IFSC."],
      "lines[0].amount": ["Enter an amount."],
      legalName: ["This name is already registered."],
    },
  });

  const state = problemToFormState(error, { aliases: { "bankAccount.ifsc": "ifsc", lines: "items" } });

  assert.deepEqual(state, {
    status: "invalid",
    fieldErrors: {
      ifsc: ["Enter a valid IFSC."],
      "items.0.amount": ["Enter an amount."],
      legalName: ["This name is already registered."],
    },
    formErrors: [],
  });
});

test("problemToFormState_BadRequestTheWebAppCausedWithoutFields_AsksToSendAgainWithTheReference", () => {
  for (const code of [
    "request.invalid",
    "request.malformed",
    "idempotency.key-missing",
    "idempotency.key-invalid",
    "query.invalid-sort",
  ]) {
    const error = apiError({ status: 400, code, detail: "SortRequest is invalid.", traceId });

    assert.deepEqual(
      problemToFormState(error),
      { status: "failed", message: formMessages.notSent, code, reference: traceId },
      code,
    );
  }
});

test("problemToFormState_AntiforgeryRefusal_AsksToSendAgainWithoutTheDetail", () => {
  for (const code of ["antiforgery.token-missing", "antiforgery.token-invalid"]) {
    const error = apiError({
      status: 400,
      type: `/problems/${code}`,
      title: "The request could not be confirmed as coming from this site.",
      detail: "A request that changes data with the session cookie must carry the X-XSRF-TOKEN header.",
      code,
      traceId,
    });

    const state = problemToFormState(error);

    assert.deepEqual(
      state,
      { status: "failed", message: formMessages.notSent, code, reference: traceId },
      code,
    );
    assert.ok(!JSON.stringify(state).includes("X-XSRF-TOKEN"), code);
  }
});

test("problemToFormState_BadRequestWithoutACode_AsksToSendAgain", () => {
  assert.deepEqual(problemToFormState(apiError({ status: 400, detail: "Bad request." })), {
    status: "failed",
    message: formMessages.notSent,
  });
});

test("problemToFormState_BadRequestWithAModuleCode_ShowsItsDetail", () => {
  const error = apiError({
    status: 400,
    code: "invoice.due-before-issue",
    detail: "The due date cannot be before the issue date.",
    traceId,
  });

  assert.deepEqual(problemToFormState(error), {
    status: "failed",
    message: "The due date cannot be before the issue date.",
    code: "invoice.due-before-issue",
    reference: traceId,
  });
});

test("problemToFormState_CodeTheFormMaps_ShowsTheFormsMessage", () => {
  const error = apiError({
    status: 409,
    code: "invoice.already-issued",
    detail: "The invoice was already issued.",
    traceId,
  });

  const state = problemToFormState(error, {
    messages: { "invoice.already-issued": "This invoice has been issued." },
  });

  assert.deepEqual(state, {
    status: "failed",
    message: "This invoice has been issued.",
    code: "invoice.already-issued",
    reference: traceId,
  });
});

test("problemToFormState_ConflictOrFailureTheFormDoesNotMap_ShowsTheDetail", () => {
  for (const status of [409, 422]) {
    const error = apiError({ status, code: "supplier.blocked", detail: "The supplier is blocked." });

    assert.deepEqual(problemToFormState(error), {
      status: "failed",
      message: "The supplier is blocked.",
      code: "supplier.blocked",
    });
  }
});

test("problemToFormState_RefusalWithoutADetail_SaysItCouldNotBeSaved", () => {
  for (const status of [409, 413, 415, 422]) {
    assert.deepEqual(problemToFormState(apiError({ status })), {
      status: "failed",
      message: formMessages.refused,
    });
  }
});

test("problemToFormState_IdempotencyOutcomes_ExplainWhatToDoNext", () => {
  const table: readonly (readonly [number, string, string])[] = [
    [409, "idempotency.in-progress", formMessages.stillSaving],
    [422, "idempotency.key-reused", formMessages.detailsChanged],
    [409, "idempotency.replay-unavailable", formMessages.alreadySaved],
  ];

  for (const [status, code, message] of table) {
    assert.deepEqual(
      problemToFormState(apiError({ status, code, detail: "Technical detail.", traceId })),
      { status: "failed", message, code, reference: traceId },
      code,
    );
  }
});

test("problemToFormState_AccessAndAvailabilityStatuses_UsePlainMessages", () => {
  const table: readonly (readonly [number, string, string])[] = [
    [401, "request.unauthenticated", formMessages.sessionEnded],
    [403, "request.forbidden", formMessages.forbidden],
    [404, "resource.not-found", formMessages.notFound],
    [404, "feature.disabled", formMessages.featureDisabled],
    [429, "rate-limit.exceeded", formMessages.tooManyRequests],
  ];

  for (const [status, code, message] of table) {
    assert.deepEqual(
      problemToFormState(apiError({ status, code, detail: "Technical detail." })),
      { status: "failed", message, code },
      code,
    );
  }
});

test("problemToFormState_ServerErrorOrTimeout_IsAFailureWithTheReferenceAndNoCode", () => {
  for (const [status, code] of [
    [500, "server.error"],
    [503, "service.unavailable"],
    [504, "request.timeout"],
  ] as const) {
    assert.deepEqual(
      problemToFormState(apiError({ status, code, detail: "Stack detail.", traceId })),
      { status: "failed", message: formMessages.serverFailed, reference: traceId },
      code,
    );
  }
});

test("problemToFormState_SuccessWithoutABody_IsAServerFailure", () => {
  const error = apiError({ status: 502, title: "The API answered without a body." });

  assert.deepEqual(problemToFormState(error), { status: "failed", message: formMessages.serverFailed });
});

test("problemToFormState_ApiUnreachable_SaysTheServiceDidNotRespond", () => {
  const refused = Object.assign(new Error("connect ECONNREFUSED 127.0.0.1:5080"), { code: "ECONNREFUSED" });
  const unknownHost = Object.assign(new Error("getaddrinfo ENOTFOUND api.internal"), { code: "ENOTFOUND" });
  const reset = Object.assign(new Error("other side closed"), { code: "UND_ERR_SOCKET" });

  for (const cause of [refused, unknownHost, reset]) {
    assert.deepEqual(
      problemToFormState(new TypeError("fetch failed", { cause })),
      { status: "failed", message: formMessages.unreachable },
      cause.message,
    );
  }
  assert.deepEqual(problemToFormState(new TypeError("terminated", { cause: reset })), {
    status: "failed",
    message: formMessages.unreachable,
  });
  assert.deepEqual(
    problemToFormState(apiError({ status: 0, title: "The upload could not reach the server." })),
    {
      status: "failed",
      message: formMessages.unreachable,
    },
  );
});

test("problemToFormState_TypeErrorThatIsNotAFetchFailure_IsRethrown", () => {
  const programmingErrors = [
    new TypeError("Cannot read properties of undefined (reading 'amount')"),
    new TypeError("Failed to parse URL from not a url", { cause: new TypeError("Invalid URL") }),
    new TypeError("fetch failed"),
    new TypeError("fetch failed", { cause: "bad port" }),
    new TypeError("fetch failed", { cause: new Error("unexpected redirect") }),
  ];

  for (const error of programmingErrors) {
    assert.throws(
      () => problemToFormState(error),
      (thrown) => thrown === error,
      error.message,
    );
  }
});

test("problemToFormState_ErrorItDoesNotKnow_IsRethrown", () => {
  const unknown = new Error("boom");
  assert.throws(
    () => problemToFormState(unknown),
    (thrown) => thrown === unknown,
  );
  assert.throws(
    () => problemToFormState("boom"),
    (thrown) => thrown === "boom",
  );

  const redirect = apiError({ status: 302 });
  assert.throws(
    () => problemToFormState(redirect),
    (thrown) => thrown === redirect,
  );
});

test("problemToFormState_CodeNamingAnInheritedMember_IsNotAFormMessage", () => {
  const error = apiError({ status: 409, code: "constructor", detail: "The record changed." });

  assert.deepEqual(problemToFormState(error, { messages: {} }), {
    status: "failed",
    message: "The record changed.",
    code: "constructor",
  });
});

test("problemToFormState_FormMessageForAPlatformCode_WinsOverTheKitsWording", () => {
  const error = apiError({ status: 409, code: "idempotency.in-progress" });

  const state = problemToFormState(error, {
    messages: { "idempotency.in-progress": "The supplier is still being saved." },
  });

  assert.deepEqual(state, {
    status: "failed",
    message: "The supplier is still being saved.",
    code: "idempotency.in-progress",
  });
});
