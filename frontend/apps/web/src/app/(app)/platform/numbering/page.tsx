import type { Metadata } from "next";

import { NumberingOverview, numberingNavigation } from "@/features/platform/numbering";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata(numberingNavigation.title, numberingNavigation.permission);
}

export default function NumberingPage() {
  return (
    <RequirePermission permission={numberingNavigation.permission}>
      <NumberingOverview />
    </RequirePermission>
  );
}
