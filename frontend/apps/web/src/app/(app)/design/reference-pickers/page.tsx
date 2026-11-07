import type { Metadata } from "next";

import { ReferencePickers } from "@/features/platform/design";

export const metadata: Metadata = { title: "Reference pickers" };

export default function ReferencePickersPage() {
  return <ReferencePickers />;
}
