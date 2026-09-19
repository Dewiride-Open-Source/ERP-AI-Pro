import { Button } from "@dewiride/erp-ui/components/ui/button";
import Link from "next/link";

export default function NotFound() {
  return (
    <main className="flex min-h-dvh flex-col items-center justify-center gap-6 px-4 text-center">
      <p className="text-sm font-semibold tracking-widest text-primary uppercase">404</p>
      <h1 className="text-3xl font-semibold tracking-tight">This page does not exist</h1>
      <p className="max-w-md text-muted-foreground">The link may be outdated or the page may have moved.</p>
      <Button asChild>
        <Link href="/">Go to sign in</Link>
      </Button>
    </main>
  );
}
