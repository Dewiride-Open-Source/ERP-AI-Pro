import type { Metadata } from "next";
import { notFound } from "next/navigation";

import { mayShow } from "@/shared/auth/permissions";
import { pageMetadata } from "@/shared/auth/require-permission";
import { requireSession } from "@/shared/auth/session";

import { approvalsNavigation } from "../../../nav";
import { getApprovalRequest } from "../../server/queries";
import { nonEmpty } from "../request-rows";

export const requestTitle = "Approval request";

// The title is the request's kind, read through the same cached request as the page, so it is ready when the page is; a
// person without access is never read for, and an unknown request, or one the person may not see, answers the not-found page.
export async function approvalRequestPageTitle(requestId: string): Promise<Metadata> {
  const { permission } = approvalsNavigation;
  if (!mayShow(await requireSession(), permission)) return pageMetadata(requestTitle, permission);
  const read = await getApprovalRequest(requestId);
  if (read.status === "missing") notFound();
  const title =
    read.status === "read" ? (nonEmpty(read.request.kindTitle) ?? nonEmpty(read.request.kind)) : undefined;
  return pageMetadata(title ?? requestTitle, permission);
}
