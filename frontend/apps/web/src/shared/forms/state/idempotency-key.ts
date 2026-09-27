import { problemCodes } from "../errors/problem-codes.ts";
import type { FormState } from "./form-state.ts";

const createAlreadyRan: ReadonlySet<string> = new Set([
  problemCodes.idempotencyInProgress,
  problemCodes.idempotencyReplayUnavailable,
]);

// A failure without a code is a transport failure or a server error, after which the API may have created the record or
// released the key; the two codes mean a create already ran under the key. Sending the same key again lets the API replay
// or finish that create instead of making a second one (docs/architecture/application-pipeline.md, "Idempotency").
export function keepsIdempotencyKey(outcome: FormState): boolean {
  return outcome.status === "failed" && (outcome.code === undefined || createAlreadyRan.has(outcome.code));
}
