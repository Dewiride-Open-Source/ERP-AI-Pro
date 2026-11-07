import type { ReactNode } from "react";

import { exchangeRatesNavigation } from "@/features/platform/reference-data";
import { requireFeature } from "@/shared/feature-flags/require-feature";

export default async function ReferenceDataLayout({ children }: Readonly<{ children: ReactNode }>) {
  await requireFeature(exchangeRatesNavigation.featureFlag);
  return children;
}
