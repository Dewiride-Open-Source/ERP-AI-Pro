import type { Metadata } from "next";

import { SystemInfoOverview, systemInfoNavigation } from "@/features/platform/system-info";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata("System information", systemInfoNavigation.permission);
}

export default function SystemInfoPage() {
  return (
    <RequirePermission permission={systemInfoNavigation.permission}>
      <SystemInfoOverview />
    </RequirePermission>
  );
}
