import type { Metadata } from "next";

import { AttachmentsOverview } from "@/features/platform/attachments";

export const metadata: Metadata = { title: "Attachments" };

export default async function AttachmentsPage({
  searchParams,
}: Readonly<PageProps<"/platform/attachments">>) {
  const { page } = await searchParams;

  return <AttachmentsOverview pageParameter={page} />;
}
