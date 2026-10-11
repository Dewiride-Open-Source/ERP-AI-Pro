import type { Metadata } from "next";

import { AttachmentsOverview, attachmentsNavigation } from "@/features/platform/attachments";
import { pageMetadata, RequirePermission } from "@/shared/auth/require-permission";

export function generateMetadata(): Promise<Metadata> {
  return pageMetadata("Attachments", attachmentsNavigation.permission);
}

export default async function AttachmentsPage({
  searchParams,
}: Readonly<PageProps<"/platform/attachments">>) {
  return (
    <RequirePermission permission={attachmentsNavigation.permission}>
      <AttachmentsOverview searchParameters={await searchParams} />
    </RequirePermission>
  );
}
