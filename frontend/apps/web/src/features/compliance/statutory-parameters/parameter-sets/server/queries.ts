import "server-only";

import { unstable_rethrow } from "next/navigation";
import { cache } from "react";

import { callApi } from "@/shared/api/client";
import { queryDateOf } from "@/shared/api/dates/query-date";
import { ApiError } from "@/shared/api/problem-details";

import {
  setDetailOf,
  setSummaryOf,
  type ParameterSetDetail,
  type ParameterSetSummary,
} from "../components/set-items";

export type ParameterSetsRead =
  { readonly status: "read"; readonly sets: readonly ParameterSetSummary[] } | { readonly status: "failed" };

export type ParameterSetRead =
  | { readonly status: "read"; readonly set: ParameterSetDetail }
  | { readonly status: "missing" }
  | { readonly status: "failed" };

// A set that cannot be read is answered as such instead of thrown, so the page says so in its own words; what rethrow passes
// on (a redirect to sign in) still propagates.
export const listParameterSets = cache(async (): Promise<ParameterSetsRead> => {
  try {
    const response = await callApi((client) => client.api.compliance.statutoryParameters.parameterSets.get());
    return { status: "read", sets: (response.sets ?? []).map(setSummaryOf) };
  } catch (error) {
    unstable_rethrow(error);
    return { status: "failed" };
  }
});

export const getParameterSet = cache(
  async (key: string, on: string | undefined): Promise<ParameterSetRead> => {
    try {
      const response = await callApi((client) =>
        client.api.compliance.statutoryParameters.parameterSets
          .byKey(key)
          .get(on === undefined ? undefined : { queryParameters: { on: queryDateOf(on) } }),
      );
      return { status: "read", set: setDetailOf(response) };
    } catch (error) {
      unstable_rethrow(error);
      return error instanceof ApiError && error.status === 404 ? { status: "missing" } : { status: "failed" };
    }
  },
);
