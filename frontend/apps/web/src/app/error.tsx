"use client";

import { ErrorPage } from "@/shared/layout/error-page";

export default function RootError({
  error,
  reset,
}: Readonly<{ error: Error & { digest?: string }; reset: () => void }>) {
  return <ErrorPage digest={error.digest} onRetry={reset} />;
}
