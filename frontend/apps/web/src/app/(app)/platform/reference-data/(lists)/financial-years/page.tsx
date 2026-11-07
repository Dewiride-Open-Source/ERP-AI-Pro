import type { Metadata } from "next";

import {
  FinancialYearsOverview,
  financialYearsTitle,
  referenceListsNavigation,
} from "@/features/platform/reference-data";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata(financialYearsTitle, referenceListsNavigation.permission);
}

export default async function FinancialYearsPage({
  searchParams,
}: Readonly<PageProps<"/platform/reference-data/financial-years">>) {
  return (
    <RequirePermission permission={referenceListsNavigation.permission}>
      <FinancialYearsOverview searchParameters={await searchParams} />
    </RequirePermission>
  );
}
