import type { Metadata } from "next";

import { approvalsNavigation, RequestBoxOverview, requestBoxPages } from "@/features/platform/approvals";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata(requestBoxPages.mine.title, approvalsNavigation.permission);
}

export default async function MyApprovalRequestsPage({
  searchParams,
}: Readonly<PageProps<"/platform/approvals/mine">>) {
  return (
    <RequirePermission permission={approvalsNavigation.permission}>
      <RequestBoxOverview box="mine" searchParameters={await searchParams} />
    </RequirePermission>
  );
}
