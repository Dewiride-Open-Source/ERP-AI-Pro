import type { Metadata } from "next";

import { OrgChartOverview, orgChartNavigation } from "@/features/identity/users";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata(orgChartNavigation.title, orgChartNavigation.permission);
}

export default async function OrgChartPage({
  searchParams,
}: Readonly<PageProps<"/identity/users/org-chart">>) {
  return (
    <RequirePermission permission={orgChartNavigation.permission}>
      <OrgChartOverview searchParameters={await searchParams} />
    </RequirePermission>
  );
}
