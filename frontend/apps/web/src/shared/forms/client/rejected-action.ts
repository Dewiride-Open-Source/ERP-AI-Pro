import { formMessages } from "../errors/form-messages.ts";
import { formFailed, type FailedFormState } from "../state/form-state.ts";

// A call from a page another deployment served names a Server Function this web server does not have: the server answers
// x-nextjs-action-not-found without running anything, Next.js rejects with UnrecognizedActionError, and only a reload
// brings the page's current Server Functions, so sending again can never succeed (Next.js, "Server Actions", Deployment
// considerations). A Server Function that threw reaches the browser as an error carrying the digest the web
// server logged it under; any other rejection means the call or its answer never arrived, so the form cannot know
// whether the create ran. None of them carries a code, so the form keeps its values and its idempotency key. Next.js's
// own redirect and not-found errors are rethrown by the caller before this runs.
export function rejectedActionState(
  error: unknown,
  isUnrecognizedAction: (error: unknown) => boolean,
): FailedFormState {
  if (isUnrecognizedAction(error)) return formFailed(formMessages.pageOutOfDate);

  const digest =
    typeof error === "object" && error !== null && "digest" in error && typeof error.digest === "string"
      ? error.digest
      : "";
  return digest === ""
    ? formFailed(formMessages.unreachable)
    : formFailed(formMessages.serverFailed, { reference: digest });
}
