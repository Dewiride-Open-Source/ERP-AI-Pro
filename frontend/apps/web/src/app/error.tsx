"use client";

import { Button } from "@dewiride/erp-ui/components/ui/button";

export default function ErrorPage({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <main className="flex min-h-dvh flex-col items-center justify-center gap-section px-gutter text-center">
      <p className="text-eyebrow text-destructive uppercase">Something went wrong</p>
      <h1 className="text-title">We could not load this page</h1>
      {error.digest ? (
        <p className="font-mono text-xs text-muted-foreground">Reference {error.digest}</p>
      ) : null}
      <Button onClick={reset}>Try again</Button>
    </main>
  );
}
