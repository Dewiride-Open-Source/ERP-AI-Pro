import { formMessages } from "../errors/form-messages.ts";
import { formFailed, type FailedFormState } from "../state/form-state.ts";

// A Server Function that threw reaches the browser as an error carrying the digest the web server logged it under; any
// other rejection means the call or its answer never arrived. Either way the form cannot know whether the create ran, so
// the failure carries no code and keeps the idempotency key. Next.js's own redirect and not-found errors are rethrown by
// the caller before this runs.
export function rejectedActionState(error: unknown): FailedFormState {
  const digest =
    typeof error === "object" && error !== null && "digest" in error && typeof error.digest === "string"
      ? error.digest
      : "";
  return digest === ""
    ? formFailed(formMessages.unreachable)
    : formFailed(formMessages.serverFailed, { reference: digest });
}
