import assert from "node:assert/strict";
import { test } from "node:test";

import { UnrecognizedActionError } from "next/dist/client/components/unrecognized-action-error.js";
import { unstable_isUnrecognizedActionError } from "next/navigation.js";

import { formMessages } from "../errors/form-messages.ts";
import { keepsIdempotencyKey } from "../state/idempotency-key.ts";
import { rejectedActionState } from "./rejected-action.ts";

const digest = "2632171185";

function serverError(message: string): Error & { digest: string } {
  return Object.assign(new Error(message), { digest });
}

function unrecognizedAction(): UnrecognizedActionError {
  return new UnrecognizedActionError('Server Action "7f3a9c0d2b" was not found on the server.');
}

function rejected(error: unknown) {
  return rejectedActionState(error, unstable_isUnrecognizedActionError);
}

test("rejectedActionState_CallThatNeverReachedTheWebServer_SaysTheServiceDidNotRespond", () => {
  for (const error of [
    new TypeError("Failed to fetch"),
    new TypeError("NetworkError when attempting to fetch resource."),
    new TypeError("Load failed"),
  ]) {
    assert.deepEqual(rejected(error), { status: "failed", message: formMessages.unreachable }, error.message);
  }
});

test("rejectedActionState_AnswerThatIsNotAServerFunctionAnswer_SaysTheServiceDidNotRespond", () => {
  assert.deepEqual(rejected(new Error("An unexpected response was received from the server.")), {
    status: "failed",
    message: formMessages.unreachable,
  });
  assert.deepEqual(rejected("aborted"), { status: "failed", message: formMessages.unreachable });
  assert.deepEqual(rejected(Object.assign(new Error("Failed"), { digest: "" })), {
    status: "failed",
    message: formMessages.unreachable,
  });
});

test("rejectedActionState_ServerFunctionThatThrew_IsAServerFailureWithTheDigestAsReference", () => {
  const error = serverError(
    "An error occurred in the Server Components render. The specific message is omitted in production builds.",
  );

  assert.deepEqual(rejected(error), {
    status: "failed",
    message: formMessages.serverFailed,
    reference: digest,
  });
});

test("rejectedActionState_ServerFunctionUnknownToTheWebServer_SaysToReloadThePage", () => {
  assert.deepEqual(rejected(unrecognizedAction()), { status: "failed", message: formMessages.pageOutOfDate });
});

test("rejectedActionState_UnrecognizedActionPredicate_IsAskedBeforeTheDigest", () => {
  const error = serverError("Refused.");

  assert.deepEqual(
    rejectedActionState(error, (candidate) => candidate === error),
    { status: "failed", message: formMessages.pageOutOfDate },
  );
});

test("rejectedActionState_AnyRejection_KeepsTheIdempotencyKey", () => {
  for (const error of [
    new TypeError("Failed to fetch"),
    serverError("Boom."),
    unrecognizedAction(),
    undefined,
  ]) {
    assert.equal(keepsIdempotencyKey(rejected(error)), true, String(error));
  }
});
