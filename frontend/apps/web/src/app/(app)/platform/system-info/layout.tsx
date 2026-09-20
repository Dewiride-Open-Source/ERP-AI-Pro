import type { ReactNode } from "react";

import { systemInfoNavigation } from "@/features/platform/system-info";
import { requireFeature } from "@/shared/feature-flags/require-feature";

export default async function SystemInfoLayout({ children }: Readonly<{ children: ReactNode }>) {
  await requireFeature(systemInfoNavigation.featureFlag);
  return children;
}
