import "server-only";

import type { JobRunHistoryResponse } from "@dewiride/erp-api-client";
import { unstable_rethrow } from "next/navigation";
import { cache } from "react";
import { z } from "zod";

import { callApi } from "@/shared/api/client";
import { ApiError } from "@/shared/api/problem-details";
import type { ListApiParameters } from "@/shared/lists/list-api";

export type RunRead =
  | { readonly status: "read"; readonly history: JobRunHistoryResponse }
  | { readonly status: "missing" }
  | { readonly status: "failed"; readonly reason: unknown };

const runId = z.uuid();

const runsPage = cache((page: number, pageSize: number, sort: string, filter: string | undefined) =>
  callApi((client) =>
    client.api.platform.jobs.runs.get({
      queryParameters: filter === undefined ? { page, pageSize, sort } : { page, pageSize, sort, filter },
    }),
  ),
);

export function getRuns({ page, pageSize, sort, filter }: ListApiParameters) {
  return runsPage(page, pageSize, sort, filter);
}

export const getRun = cache(async (id: string): Promise<RunRead> => {
  if (!runId.safeParse(id).success) return { status: "missing" };
  try {
    return {
      status: "read",
      history: await callApi((client) => client.api.platform.jobs.runs.byId(id).get()),
    };
  } catch (error) {
    unstable_rethrow(error);
    return error instanceof ApiError && error.status === 404
      ? { status: "missing" }
      : { status: "failed", reason: error };
  }
});
