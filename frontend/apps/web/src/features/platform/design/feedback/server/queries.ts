import "server-only";

import { cookies } from "next/headers";

import type { ListQuery } from "@/shared/lists/list-query";

export interface ApprovalRequest {
  readonly id: string;
  readonly request: string;
  readonly requestedBy: string;
  readonly raisedOn: string;
  readonly locked: boolean;
}

export interface ApprovalRequestPage {
  readonly items: readonly ApprovalRequest[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
}

export const dismissedApprovalsCookie = "erp-design-dismissed-approvals";

export const approvalRequests: readonly ApprovalRequest[] = [
  {
    id: "APR-0101",
    request: "Purchase order PO-2026-0412 for laptops",
    requestedBy: "Anita Rao",
    raisedOn: "2026-09-21",
    locked: false,
  },
  {
    id: "APR-0102",
    request: "Travel advance for the Pune client visit",
    requestedBy: "Vikram Singh",
    raisedOn: "2026-09-22",
    locked: false,
  },
  {
    id: "APR-0103",
    request: "Onboarding of Konark Electricals as a vendor",
    requestedBy: "Meera Iyer",
    raisedOn: "2026-09-23",
    locked: true,
  },
  {
    id: "APR-0104",
    request: "Budget revision for the festive campaign",
    requestedBy: "Rahul Verma",
    raisedOn: "2026-09-24",
    locked: false,
  },
  {
    id: "APR-0105",
    request: "Renewal of the design software subscription",
    requestedBy: "Farhan Ali",
    raisedOn: "2026-09-25",
    locked: false,
  },
];

export function readDismissedApprovals(value: string | undefined): ReadonlySet<string> {
  const known = new Set(approvalRequests.map((request) => request.id));
  return new Set((value ?? "").split(",").filter((id) => known.has(id)));
}

export async function getApprovalRequests(query: ListQuery): Promise<ApprovalRequestPage> {
  const dismissed = readDismissedApprovals((await cookies()).get(dismissedApprovalsCookie)?.value);
  const field = query.sort.field === "requestedBy" ? "requestedBy" : "raisedOn";
  const direction = query.sort.direction === "asc" ? 1 : -1;
  const remaining = approvalRequests
    .filter((request) => !dismissed.has(request.id))
    .toSorted((first, second) => direction * first[field].localeCompare(second[field], "en-IN"));
  const totalPages = Math.ceil(remaining.length / query.pageSize);
  const start = (query.page - 1) * query.pageSize;
  return {
    items: remaining.slice(start, start + query.pageSize),
    page: query.page,
    pageSize: query.pageSize,
    totalCount: remaining.length,
    totalPages,
  };
}
