import { Button } from "@dewiride/erp-ui/components/ui/button";

export function ErrorPage({ digest, onRetry }: { digest: string | undefined; onRetry: () => void }) {
  return (
    <main className="flex min-h-dvh flex-col items-center justify-center gap-section px-gutter text-center">
      <p className="text-eyebrow text-destructive uppercase">Something went wrong</p>
      <h1 className="text-title">We could not load this page</h1>
      {digest ? <p className="font-mono text-xs text-muted-foreground">Reference {digest}</p> : null}
      <Button onClick={onRetry}>Try again</Button>
    </main>
  );
}
