import "server-only";

import type {
  ApprovalRequestResponse,
  PagedResponseOfApprovalRequestSummaryResponse,
} from "@dewiride/erp-api-client";
import { unstable_rethrow } from "next/navigation";
import { cache } from "react";
import { z } from "zod";

import { callApi } from "@/shared/api/client";
import { ApiError } from "@/shared/api/problem-details";
import type { ListApiParameters } from "@/shared/lists/list-api";

import type { RequestCounts } from "../components/request-text";
import type { RequestBox } from "../lists/requests.list";

export type RequestsRead =
  | { readonly status: "read"; readonly list: PagedResponseOfApprovalRequestSummaryResponse }
  | { readonly status: "failed"; readonly reason: unknown };

export type RequestRead =
  | { readonly status: "read"; readonly request: ApprovalRequestResponse }
  | { readonly status: "missing" }
  | { readonly status: "failed"; readonly reason: unknown };

const requestId = z.guid();

const requestsPage = cache(
  (box: RequestBox, page: number, pageSize: number, sort: string, filter: string | undefined) =>
    callApi((client) =>
      client.api.platform.approvals.requests.get({
        queryParameters:
          filter === undefined ? { box, page, pageSize, sort } : { box, page, pageSize, sort, filter },
      }),
    ),
);

export async function readApprovalRequests(
  box: RequestBox,
  { page, pageSize, sort, filter }: ListApiParameters,
): Promise<RequestsRead> {
  try {
    return { status: "read", list: await requestsPage(box, page, pageSize, sort, filter) };
  } catch (error) {
    unstable_rethrow(error);
    return { status: "failed", reason: error };
  }
}

// The counts only add to what a list shows, so a page whose counts cannot be read shows its list without them.
export const readApprovalCounts = cache(async (): Promise<RequestCounts | undefined> => {
  try {
    const { waitingForMe, lateForMe, mineWaiting } = await callApi((client) =>
      client.api.platform.approvals.requests.counts.get(),
    );
    return typeof waitingForMe === "number" &&
      typeof lateForMe === "number" &&
      typeof mineWaiting === "number"
      ? { waitingForMe, lateForMe, mineWaiting }
      : undefined;
  } catch (error) {
    unstable_rethrow(error);
    return undefined;
  }
});

export const getApprovalRequest = cache(async (id: string): Promise<RequestRead> => {
  if (!requestId.safeParse(id).success) return { status: "missing" };
  try {
    return {
      status: "read",
      request: await callApi((client) => client.api.platform.approvals.requests.byId(id).get()),
    };
  } catch (error) {
    unstable_rethrow(error);
    return error instanceof ApiError && error.status === 404
      ? { status: "missing" }
      : { status: "failed", reason: error };
  }
});
