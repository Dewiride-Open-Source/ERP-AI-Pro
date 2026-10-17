import type { Metadata } from "next";

import { PermissionsOverview, peopleNavigation } from "@/features/identity/users";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata("Roles and permissions", peopleNavigation.permission);
}

export default function PermissionsPage() {
  return (
    <RequirePermission permission={peopleNavigation.permission}>
      <PermissionsOverview />
    </RequirePermission>
  );
}
