import type { ReactNode } from "react";

import { numberingNavigation } from "@/features/platform/numbering";
import { requireFeature } from "@/shared/feature-flags/require-feature";

export default async function NumberingLayout({ children }: Readonly<{ children: ReactNode }>) {
  await requireFeature(numberingNavigation.featureFlag);
  return children;
}
