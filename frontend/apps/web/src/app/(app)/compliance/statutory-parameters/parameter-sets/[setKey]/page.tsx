import type { Metadata } from "next";

import {
  ParameterSetOverview,
  parameterSetPageTitle,
  parameterSetsNavigation,
} from "@/features/compliance/statutory-parameters";
import { RequirePermission } from "@/shared/auth/require-permission";

export async function generateMetadata({
  params,
  searchParams,
}: PageProps<"/compliance/statutory-parameters/parameter-sets/[setKey]">): Promise<Metadata> {
  return parameterSetPageTitle((await params).setKey, await searchParams);
}

export default async function ParameterSetPage({
  params,
  searchParams,
}: Readonly<PageProps<"/compliance/statutory-parameters/parameter-sets/[setKey]">>) {
  const { setKey } = await params;
  return (
    <RequirePermission permission={parameterSetsNavigation.permission}>
      <ParameterSetOverview setKey={setKey} searchParameters={await searchParams} />
    </RequirePermission>
  );
}
