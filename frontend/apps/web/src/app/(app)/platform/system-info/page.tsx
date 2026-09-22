import type { Metadata } from "next";

import { RecentStartupsTable, SystemInfoCard } from "@/features/platform/system-info";

export const metadata: Metadata = { title: "System information" };

export default function SystemInfoPage() {
  return (
    <div className="grid gap-6">
      <SystemInfoCard />
      <RecentStartupsTable />
    </div>
  );
}
