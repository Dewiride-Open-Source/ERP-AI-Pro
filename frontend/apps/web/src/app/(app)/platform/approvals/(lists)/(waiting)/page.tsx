import type { Metadata } from "next";

import { approvalsNavigation, RequestBoxOverview, requestBoxPages } from "@/features/platform/approvals";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata(requestBoxPages.waiting.title, approvalsNavigation.permission);
}

export default async function WaitingApprovalsPage({
  searchParams,
}: Readonly<PageProps<"/platform/approvals">>) {
  return (
    <RequirePermission permission={approvalsNavigation.permission}>
      <RequestBoxOverview box="waiting" searchParameters={await searchParams} />
    </RequirePermission>
  );
}
