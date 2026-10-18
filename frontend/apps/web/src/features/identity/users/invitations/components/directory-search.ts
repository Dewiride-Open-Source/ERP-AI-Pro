import type { DirectoryPersonResponse } from "@dewiride/erp-api-client";

import type { Problem } from "@/shared/api/problem-details";
import { formFailed, type FailedFormState } from "@/shared/forms/state/form-state";

export const directorySearchMinLength = 2;

export const directorySearchMaxLength = 100;

export type DirectoryMatch = {
  readonly entraObjectId: string;
  readonly displayName: string;
  readonly signInName: string;
  readonly personId?: string | undefined;
};

export type DirectorySearchAnswer =
  | { readonly people: readonly DirectoryMatch[]; readonly hasMore: boolean }
  | { readonly error: string; readonly reference?: string | undefined };

export type SearchText =
  | { readonly status: "empty" }
  | { readonly status: "short" }
  | { readonly status: "refused"; readonly message: string }
  | { readonly status: "ready"; readonly text: string };

export type SearchProgress = {
  readonly loading: boolean;
  readonly answer: DirectorySearchAnswer | undefined;
};

export const searchHint = "At least 2 characters of their name or email address.";

export const searchTooShort = "Type at least 2 characters to search.";

export const searchHasAmpersand = "Leave out the & sign: the company directory cannot search for it.";

export const searchHasHiddenCharacter = "Remove the tab or other hidden character from the search.";

export const searchTooLong = `Use ${directorySearchMaxLength} characters or fewer.`;

export const searching = "Searching the directory…";

export const nobodyMatches = "Nobody in the directory matches.";

export const moreMatch = "More people match; type more of the name.";

export const alreadyInTheErp = "Already in the ERP";

export const directoryAccessDenied =
  "Microsoft Entra did not let the ERP read the company directory for you. Sign in again and try once more; if it keeps happening, the organisation's consent for the ERP is missing in Microsoft Entra.";

export const directoryUnavailable = "The company directory cannot be read right now. Try again in a moment.";

export const directoryTimedOut = "The company directory took too long to answer. Try again in a moment.";

const controlCharacter = /\p{Cc}/u;

// The API's rule for the search text (SearchDirectoryRequest), applied to the trimmed text the browser then sends: Microsoft
// Graph refuses an ampersand in $search even when it is encoded (ADR-0040).
export function readSearchText(text: string): SearchText {
  const trimmed = text.trim();
  if (trimmed === "") return { status: "empty" };
  if (controlCharacter.test(trimmed)) return { status: "refused", message: searchHasHiddenCharacter };
  if (trimmed.includes("&")) return { status: "refused", message: searchHasAmpersand };
  if (trimmed.length > directorySearchMaxLength) return { status: "refused", message: searchTooLong };
  if (trimmed.length < directorySearchMinLength) return { status: "short" };
  return { status: "ready", text: trimmed };
}

export function searchSummary(reading: SearchText, { loading, answer }: SearchProgress): string {
  if (reading.status === "short") return searchTooShort;
  if (reading.status !== "ready") return "";
  if (loading) return searching;
  if (answer === undefined || "error" in answer) return "";
  if (answer.people.length === 0) return nobodyMatches;
  const found = answer.people.length === 1 ? "1 person found." : `${answer.people.length} people found.`;
  return answer.hasMore ? `${found} ${moreMatch}` : found;
}

export function directoryMatches(people: readonly DirectoryPersonResponse[]): DirectoryMatch[] {
  return people.flatMap(({ entraObjectId, displayName, signInName, personId }) =>
    entraObjectId && displayName && signInName
      ? [{ entraObjectId, displayName, signInName, personId: personId ?? undefined }]
      : [],
  );
}

// The directory is read on the signed-in administrator's behalf, so Microsoft Entra may refuse it, and Graph may be down or
// slow (ADR-0040), which the forms kit would word as a fault of the ERP. The API stores a refusal under the idempotency key,
// so it carries its code and an invitation sent again takes a new key; a 5xx answer releases the key and a request that ran
// out of time may still have saved, so those carry none and the same key goes again.
export function directoryFailure({
  status,
  problem,
}: {
  readonly status: number;
  readonly problem: Problem;
}): FailedFormState | undefined {
  if (status === 403 && problem.code === "directory.access-denied") {
    return formFailed(directoryAccessDenied, { code: problem.code, reference: problem.traceId });
  }
  if (status === 503 && problem.code === "directory.unavailable") {
    return formFailed(directoryUnavailable, { reference: problem.traceId });
  }
  if (status === 504 && problem.code === "request.timeout") {
    return formFailed(directoryTimedOut, { reference: problem.traceId });
  }
  return undefined;
}
