import type { Metadata } from "next";

import { FeedbackDemo } from "@/features/platform/design";

export const metadata: Metadata = { title: "Feedback" };

export default async function FeedbackPage({ searchParams }: Readonly<PageProps<"/design/feedback">>) {
  return <FeedbackDemo searchParameters={await searchParams} />;
}
