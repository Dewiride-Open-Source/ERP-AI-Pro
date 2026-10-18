import type { Metadata } from "next";

import { DepartmentsOverview, departmentsNavigation } from "@/features/identity/users";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata(departmentsNavigation.title, departmentsNavigation.permission);
}

export default function DepartmentsPage() {
  return (
    <RequirePermission permission={departmentsNavigation.permission}>
      <DepartmentsOverview />
    </RequirePermission>
  );
}
