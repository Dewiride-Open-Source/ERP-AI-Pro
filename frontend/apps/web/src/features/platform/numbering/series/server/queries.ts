import "server-only";

import { unstable_rethrow } from "next/navigation";
import { cache } from "react";

import { callApi } from "@/shared/api/client";
import { ApiError } from "@/shared/api/problem-details";

import {
  numberedDocumentOf,
  UnreadableAnswerError,
  type NumberedDocument,
} from "../components/numbering-items";

export type NumberedDocumentsRead =
  | { readonly status: "read"; readonly documents: readonly NumberedDocument[] }
  | { readonly status: "failed"; readonly reason: string };

// The kinds of document are answered as unreadable instead of thrown, so the page says so in its own words; what rethrow passes
// on (a redirect to sign in) still propagates.
export const listNumberedDocuments = cache(async (): Promise<NumberedDocumentsRead> => {
  try {
    const documents = await callApi((client) => client.api.platform.numbering.documents.get());
    return { status: "read", documents: documents.map(numberedDocumentOf) };
  } catch (error) {
    unstable_rethrow(error);
    return { status: "failed", reason: describeFailure(error) };
  }
});

function describeFailure(error: unknown): string {
  if (error instanceof ApiError) return error.message;
  return error instanceof UnreadableAnswerError
    ? "The API's answer could not be read."
    : "The API did not respond.";
}
