import type { Metadata } from "next";

import { JobRunOverview, jobsNavigation, runPageTitle } from "@/features/platform/jobs";
import { RequirePermission } from "@/shared/auth/require-permission";

export async function generateMetadata({
  params,
}: PageProps<"/platform/jobs/runs/[runId]">): Promise<Metadata> {
  return runPageTitle((await params).runId);
}

export default async function JobRunPage({ params }: Readonly<PageProps<"/platform/jobs/runs/[runId]">>) {
  const { runId } = await params;
  return (
    <RequirePermission permission={jobsNavigation.permission}>
      <JobRunOverview runId={runId} />
    </RequirePermission>
  );
}
