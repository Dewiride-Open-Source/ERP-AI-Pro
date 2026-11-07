import type { Metadata } from "next";

import { ParameterSetsOverview, parameterSetsNavigation } from "@/features/compliance/statutory-parameters";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata(parameterSetsNavigation.title, parameterSetsNavigation.permission);
}

export default function ParameterSetsPage() {
  return (
    <RequirePermission permission={parameterSetsNavigation.permission}>
      <ParameterSetsOverview />
    </RequirePermission>
  );
}
