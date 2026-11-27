import type { Metadata } from "next";

import { RequestBoxOverview, requestBoxPages } from "@/features/platform/approvals";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata(requestBoxPages.all.title, "platform.approvals.requests.read-all");
}

export default async function AllApprovalRequestsPage({
  searchParams,
}: Readonly<PageProps<"/platform/approvals/all">>) {
  return (
    <RequirePermission permission="platform.approvals.requests.read-all">
      <RequestBoxOverview box="all" searchParameters={await searchParams} />
    </RequirePermission>
  );
}
