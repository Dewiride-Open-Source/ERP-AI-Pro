"use client";

import { ErrorPage } from "@/shared/layout/error-page";

export default function RootError({
  error,
  retry,
}: Readonly<{ error: Error & { digest?: string }; retry: () => void }>) {
  return <ErrorPage digest={error.digest} onRetry={retry} />;
}
