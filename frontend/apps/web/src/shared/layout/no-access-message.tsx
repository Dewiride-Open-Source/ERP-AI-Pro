import { Button } from "@dewiride/erp-ui/components/ui/button";
import Link from "next/link";

export function NoAccessMessage() {
  return (
    <div className="flex flex-col items-center justify-center gap-section px-gutter py-16 text-center">
      <h1 className="text-title">You do not have access to this page</h1>
      <p className="max-w-md text-muted-foreground">Ask an administrator for access.</p>
      <Button asChild>
        <Link href="/">Go to the home page</Link>
      </Button>
    </div>
  );
}
