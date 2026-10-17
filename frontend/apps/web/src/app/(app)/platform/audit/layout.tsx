import type { ReactNode } from "react";

import { auditNavigation } from "@/features/platform/audit";
import { requireFeature } from "@/shared/feature-flags/require-feature";

export default async function AuditTrailLayout({ children }: Readonly<{ children: ReactNode }>) {
  await requireFeature(auditNavigation.featureFlag);
  return children;
}
