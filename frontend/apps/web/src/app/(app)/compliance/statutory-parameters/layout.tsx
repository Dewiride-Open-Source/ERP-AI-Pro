import type { ReactNode } from "react";

import { parameterSetsNavigation } from "@/features/compliance/statutory-parameters";
import { requireFeature } from "@/shared/feature-flags/require-feature";

export default async function StatutoryParametersLayout({ children }: Readonly<{ children: ReactNode }>) {
  await requireFeature(parameterSetsNavigation.featureFlag);
  return children;
}
