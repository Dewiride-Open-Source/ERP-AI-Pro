import { problemCodes } from "../errors/problem-codes.ts";
import type { FormState } from "./form-state.ts";

export type SentIdempotencyKey = { readonly key: string; readonly after: FormState };

const createAlreadyRan: ReadonlySet<string> = new Set([
  problemCodes.idempotencyInProgress,
  problemCodes.idempotencyReplayUnavailable,
]);

// A failure without a code leaves the outcome unknown: the web server or the API did not answer, the API answered 5xx or
// a 4xx whose problem the generated client could not read, or the Server Function threw, so the API may have created the
// record or released the key (keeping a key the API never saw is harmless). The two codes mean a create already ran under
// the key. Sending the same key again lets the API replay or finish that create instead of making a second one
// (docs/architecture/application-pipeline.md, "Idempotency").
export function keepsIdempotencyKey(outcome: FormState): boolean {
  return outcome.status === "failed" && (outcome.code === undefined || createAlreadyRan.has(outcome.code));
}

export function idempotencyKeyToSend(
  last: SentIdempotencyKey | undefined,
  answer: FormState,
  mint: () => string,
): SentIdempotencyKey {
  const sameAttempt = last !== undefined && (last.after === answer || keepsIdempotencyKey(answer));
  return { key: sameAttempt ? last.key : mint(), after: answer };
}
