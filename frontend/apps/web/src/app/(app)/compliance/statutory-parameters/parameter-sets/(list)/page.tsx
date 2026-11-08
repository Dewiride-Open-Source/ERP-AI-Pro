import type { Metadata } from "next";

import { ParameterSetsOverview, parameterSetsNavigation } from "@/features/compliance/statutory-parameters";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata(parameterSetsNavigation.title, parameterSetsNavigation.permission);
}

export default async function ParameterSetsPage({
  searchParams,
}: Readonly<PageProps<"/compliance/statutory-parameters/parameter-sets">>) {
  return (
    <RequirePermission permission={parameterSetsNavigation.permission}>
      <ParameterSetsOverview searchParameters={await searchParams} />
    </RequirePermission>
  );
}
