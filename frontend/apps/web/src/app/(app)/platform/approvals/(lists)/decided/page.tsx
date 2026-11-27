import type { Metadata } from "next";

import { approvalsNavigation, RequestBoxOverview, requestBoxPages } from "@/features/platform/approvals";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata(requestBoxPages.decided.title, approvalsNavigation.permission);
}

export default async function DecidedApprovalRequestsPage({
  searchParams,
}: Readonly<PageProps<"/platform/approvals/decided">>) {
  return (
    <RequirePermission permission={approvalsNavigation.permission}>
      <RequestBoxOverview box="decided" searchParameters={await searchParams} />
    </RequirePermission>
  );
}
