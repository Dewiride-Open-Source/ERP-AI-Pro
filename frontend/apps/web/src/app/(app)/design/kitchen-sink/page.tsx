import type { Metadata } from "next";

import { KitchenSink } from "@/features/platform/design";

export const metadata: Metadata = { title: "Design system" };

export default function KitchenSinkPage() {
  return <KitchenSink />;
}
