import type { Metadata } from "next";

import { DesignationsOverview, designationsNavigation } from "@/features/identity/users";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata(designationsNavigation.title, designationsNavigation.permission);
}

export default function DesignationsPage() {
  return (
    <RequirePermission permission={designationsNavigation.permission}>
      <DesignationsOverview />
    </RequirePermission>
  );
}
