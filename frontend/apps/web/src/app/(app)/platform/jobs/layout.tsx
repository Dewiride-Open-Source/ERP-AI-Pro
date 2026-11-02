import type { ReactNode } from "react";

import { jobsNavigation } from "@/features/platform/jobs";
import { requireFeature } from "@/shared/feature-flags/require-feature";

export default async function JobsLayout({ children }: Readonly<{ children: ReactNode }>) {
  await requireFeature(jobsNavigation.featureFlag);
  return children;
}
