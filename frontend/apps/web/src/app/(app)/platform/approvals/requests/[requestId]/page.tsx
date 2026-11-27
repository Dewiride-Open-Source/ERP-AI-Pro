import type { Metadata } from "next";

import {
  ApprovalRequestOverview,
  approvalRequestPageTitle,
  approvalsNavigation,
} from "@/features/platform/approvals";
import { RequirePermission } from "@/shared/auth/require-permission";

export async function generateMetadata({
  params,
}: PageProps<"/platform/approvals/requests/[requestId]">): Promise<Metadata> {
  return approvalRequestPageTitle((await params).requestId);
}

export default async function ApprovalRequestPage({
  params,
}: Readonly<PageProps<"/platform/approvals/requests/[requestId]">>) {
  const { requestId } = await params;
  return (
    <RequirePermission permission={approvalsNavigation.permission}>
      <ApprovalRequestOverview requestId={requestId} />
    </RequirePermission>
  );
}
