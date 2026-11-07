import type { Metadata } from "next";

import {
  CurrenciesOverview,
  currenciesTitle,
  referenceListsNavigation,
} from "@/features/platform/reference-data";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata(currenciesTitle, referenceListsNavigation.permission);
}

export default async function CurrenciesPage({
  searchParams,
}: Readonly<PageProps<"/platform/reference-data/currencies">>) {
  return (
    <RequirePermission permission={referenceListsNavigation.permission}>
      <CurrenciesOverview searchParameters={await searchParams} />
    </RequirePermission>
  );
}
