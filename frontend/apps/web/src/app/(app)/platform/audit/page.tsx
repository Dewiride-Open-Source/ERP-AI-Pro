import type { Metadata } from "next";

import { AuditTrailOverview, auditNavigation } from "@/features/platform/audit";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata("Audit trail", auditNavigation.permission);
}

export default async function AuditTrailPage({ searchParams }: Readonly<PageProps<"/platform/audit">>) {
  return (
    <RequirePermission permission={auditNavigation.permission}>
      <AuditTrailOverview searchParameters={await searchParams} />
    </RequirePermission>
  );
}
