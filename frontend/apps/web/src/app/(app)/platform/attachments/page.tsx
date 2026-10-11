import type { Metadata } from "next";

import { AttachmentsOverview, attachmentsNavigation } from "@/features/platform/attachments";
import { RequirePermission } from "@/shared/auth/require-permission";

export const metadata: Metadata = { title: "Attachments" };

export default async function AttachmentsPage({
  searchParams,
}: Readonly<PageProps<"/platform/attachments">>) {
  return (
    <RequirePermission permission={attachmentsNavigation.permission}>
      <AttachmentsOverview searchParameters={await searchParams} />
    </RequirePermission>
  );
}
