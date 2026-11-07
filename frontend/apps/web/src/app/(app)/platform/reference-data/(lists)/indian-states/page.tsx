import type { Metadata } from "next";

import {
  IndianStatesOverview,
  indianStatesTitle,
  referenceListsNavigation,
} from "@/features/platform/reference-data";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata(indianStatesTitle, referenceListsNavigation.permission);
}

export default async function IndianStatesPage({
  searchParams,
}: Readonly<PageProps<"/platform/reference-data/indian-states">>) {
  return (
    <RequirePermission permission={referenceListsNavigation.permission}>
      <IndianStatesOverview searchParameters={await searchParams} />
    </RequirePermission>
  );
}
