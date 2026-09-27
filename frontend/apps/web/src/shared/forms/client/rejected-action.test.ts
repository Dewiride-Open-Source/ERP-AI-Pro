import assert from "node:assert/strict";
import { test } from "node:test";

import { formMessages } from "../errors/form-messages.ts";
import { keepsIdempotencyKey } from "../state/idempotency-key.ts";
import { rejectedActionState } from "./rejected-action.ts";

const digest = "2632171185";

function serverError(message: string): Error & { digest: string } {
  return Object.assign(new Error(message), { digest });
}

test("rejectedActionState_CallThatNeverReachedTheWebServer_SaysTheServiceDidNotRespond", () => {
  for (const error of [
    new TypeError("Failed to fetch"),
    new TypeError("NetworkError when attempting to fetch resource."),
    new TypeError("Load failed"),
  ]) {
    assert.deepEqual(
      rejectedActionState(error),
      { status: "failed", message: formMessages.unreachable },
      error.message,
    );
  }
});

test("rejectedActionState_AnswerThatIsNotAServerFunctionAnswer_SaysTheServiceDidNotRespond", () => {
  assert.deepEqual(rejectedActionState(new Error("An unexpected response was received from the server.")), {
    status: "failed",
    message: formMessages.unreachable,
  });
  assert.deepEqual(rejectedActionState("aborted"), { status: "failed", message: formMessages.unreachable });
  assert.deepEqual(rejectedActionState(Object.assign(new Error("Failed"), { digest: "" })), {
    status: "failed",
    message: formMessages.unreachable,
  });
});

test("rejectedActionState_ServerFunctionThatThrew_IsAServerFailureWithTheDigestAsReference", () => {
  const error = serverError(
    "An error occurred in the Server Components render. The specific message is omitted in production builds.",
  );

  assert.deepEqual(rejectedActionState(error), {
    status: "failed",
    message: formMessages.serverFailed,
    reference: digest,
  });
});

test("rejectedActionState_AnyRejection_KeepsTheIdempotencyKey", () => {
  for (const error of [new TypeError("Failed to fetch"), serverError("Boom."), undefined]) {
    assert.equal(keepsIdempotencyKey(rejectedActionState(error)), true, String(error));
  }
});
