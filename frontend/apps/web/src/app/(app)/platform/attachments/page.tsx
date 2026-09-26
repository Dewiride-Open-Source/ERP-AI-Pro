import type { Metadata } from "next";

import { AttachmentsOverview } from "@/features/platform/attachments";

export const metadata: Metadata = { title: "Attachments" };

export default async function AttachmentsPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const { page } = await searchParams;
  const requested = Number.parseInt(typeof page === "string" ? page : "", 10);

  return <AttachmentsOverview page={Number.isInteger(requested) && requested > 0 ? requested : 1} />;
}
