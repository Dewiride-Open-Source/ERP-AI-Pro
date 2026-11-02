import type { Metadata } from "next";

import { JobsOverview, jobsNavigation } from "@/features/platform/jobs";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata("Background jobs", jobsNavigation.permission);
}

export default async function JobsPage({ searchParams }: Readonly<PageProps<"/platform/jobs">>) {
  return (
    <RequirePermission permission={jobsNavigation.permission}>
      <JobsOverview searchParameters={await searchParams} />
    </RequirePermission>
  );
}
