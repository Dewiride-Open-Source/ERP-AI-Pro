import "server-only";

import { unstable_rethrow } from "next/navigation";
import { cache } from "react";

import { callApi } from "@/shared/api/client";
import { ApiError } from "@/shared/api/problem-details";

import { approvalKindOf, UnreadableAnswerError, type ApprovalKindItem } from "../components/kind-items";

export type KindsRead =
  | { readonly status: "read"; readonly kinds: readonly ApprovalKindItem[] }
  | { readonly status: "failed"; readonly reason: string };

// The kinds are answered as unreadable instead of thrown, so each page says so in its own words; what rethrow passes on (a
// redirect to sign in) still propagates.
export const listApprovalKinds = cache(async (): Promise<KindsRead> => {
  try {
    const kinds = await callApi((client) => client.api.platform.approvals.kinds.get());
    return { status: "read", kinds: kinds.map(approvalKindOf) };
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
