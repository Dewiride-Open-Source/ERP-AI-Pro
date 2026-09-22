import { RecentStartupsTable } from "../../startups/components/recent-startups-table";

import { SystemInfoCard } from "./system-info-card";

export function SystemInfoOverview() {
  return (
    <div className="grid gap-6">
      <SystemInfoCard />
      <RecentStartupsTable />
    </div>
  );
}
