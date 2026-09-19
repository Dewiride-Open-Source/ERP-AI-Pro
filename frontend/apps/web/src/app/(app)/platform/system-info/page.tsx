import type { Metadata } from "next";

import { SystemInfoCard } from "@/features/platform/system-info";

export const metadata: Metadata = { title: "System information" };

export default function SystemInfoPage() {
  return <SystemInfoCard />;
}
