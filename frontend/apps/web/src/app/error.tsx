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
    <main className="flex min-h-dvh flex-col items-center justify-center gap-6 px-4 text-center">
      <p className="text-sm font-semibold tracking-widest text-destructive uppercase">Something went wrong</p>
      <h1 className="text-3xl font-semibold tracking-tight">We could not load this page</h1>
      {error.digest ? (
        <p className="font-mono text-xs text-muted-foreground">Reference {error.digest}</p>
      ) : null}
      <Button onClick={reset}>Try again</Button>
    </main>
  );
}
