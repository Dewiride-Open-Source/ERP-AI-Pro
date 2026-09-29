import type { Metadata } from "next";

import { SlowReport } from "@/features/platform/design";

export const metadata: Metadata = { title: "Receivables ageing" };

export default function SlowReportPage() {
  return <SlowReport />;
}
