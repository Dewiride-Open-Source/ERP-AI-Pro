import type { ReactNode } from "react";

import { approvalsNavigation } from "@/features/platform/approvals";
import { requireFeature } from "@/shared/feature-flags/require-feature";

export default async function ApprovalsLayout({ children }: Readonly<{ children: ReactNode }>) {
  await requireFeature(approvalsNavigation.featureFlag);
  return children;
}
