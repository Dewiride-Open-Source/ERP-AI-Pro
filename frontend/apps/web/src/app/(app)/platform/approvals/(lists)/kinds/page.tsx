import type { Metadata } from "next";

import { approvalsNavigation, KindsOverview, kindsTitle } from "@/features/platform/approvals";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata(kindsTitle, approvalsNavigation.permission);
}

export default function ApprovalKindsPage() {
  return (
    <RequirePermission permission={approvalsNavigation.permission}>
      <KindsOverview />
    </RequirePermission>
  );
}
