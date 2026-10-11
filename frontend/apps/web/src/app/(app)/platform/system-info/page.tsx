import type { Metadata } from "next";

import { SystemInfoOverview, systemInfoNavigation } from "@/features/platform/system-info";
import { RequirePermission } from "@/shared/auth/require-permission";

export const metadata: Metadata = { title: "System information" };

export default function SystemInfoPage() {
  return (
    <RequirePermission permission={systemInfoNavigation.permission}>
      <SystemInfoOverview />
    </RequirePermission>
  );
}
