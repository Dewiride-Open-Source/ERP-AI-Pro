import type { Metadata } from "next";

import { DataTableDemo } from "@/features/platform/design";

export const metadata: Metadata = { title: "Data table" };

export default async function DataTablePage({ searchParams }: Readonly<PageProps<"/design/data-table">>) {
  return <DataTableDemo searchParameters={await searchParams} />;
}
