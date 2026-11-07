import type { Metadata } from "next";

import {
  CountriesOverview,
  countriesTitle,
  referenceListsNavigation,
} from "@/features/platform/reference-data";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata(countriesTitle, referenceListsNavigation.permission);
}

export default async function CountriesPage({
  searchParams,
}: Readonly<PageProps<"/platform/reference-data">>) {
  return (
    <RequirePermission permission={referenceListsNavigation.permission}>
      <CountriesOverview searchParameters={await searchParams} />
    </RequirePermission>
  );
}
