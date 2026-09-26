import type { ReactNode } from "react";

import { attachmentsNavigation } from "@/features/platform/attachments";
import { requireFeature } from "@/shared/feature-flags/require-feature";

export default async function AttachmentsLayout({ children }: Readonly<{ children: ReactNode }>) {
  await requireFeature(attachmentsNavigation.featureFlag);
  return children;
}
