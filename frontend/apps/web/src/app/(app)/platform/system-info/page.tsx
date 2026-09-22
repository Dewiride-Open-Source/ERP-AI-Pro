import type { Metadata } from "next";

import { SystemInfoOverview } from "@/features/platform/system-info";

export const metadata: Metadata = { title: "System information" };

export default function SystemInfoPage() {
  return <SystemInfoOverview />;
}
