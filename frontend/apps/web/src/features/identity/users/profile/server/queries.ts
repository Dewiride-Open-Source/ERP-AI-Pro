import "server-only";

import type { ProfileResponse } from "@dewiride/erp-api-client";
import { unstable_rethrow } from "next/navigation";
import { cache } from "react";

import { callApi } from "@/shared/api/client";
import { ApiError } from "@/shared/api/problem-details";

export type OwnProfileRead =
  | { readonly status: "read"; readonly profile: ProfileResponse }
  | { readonly status: "missing" }
  | { readonly status: "disabled" }
  | { readonly status: "failed"; readonly reason: unknown };

const moduleDisabledCode = "feature.disabled";

// The shell's theme keeper and My profile read the same answer once per request.
export const getOwnProfile = cache(async (): Promise<OwnProfileRead> => {
  try {
    return { status: "read", profile: await callApi((client) => client.api.identity.users.me.get()) };
  } catch (error) {
    unstable_rethrow(error);
    if (error instanceof ApiError && error.status === 404) {
      return error.problem.code === moduleDisabledCode ? { status: "disabled" } : { status: "missing" };
    }
    return { status: "failed", reason: error };
  }
});
