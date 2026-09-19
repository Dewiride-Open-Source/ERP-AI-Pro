"use client";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import { RefreshCwIcon } from "lucide-react";
import { useRouter } from "next/navigation";
import { useTransition } from "react";

export function RefreshButton() {
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  return (
    <Button
      variant="outline"
      onClick={() => startTransition(() => router.refresh())}
      disabled={pending}
      data-testid="system-info-refresh"
    >
      <RefreshCwIcon className={pending ? "animate-spin" : ""} aria-hidden />
      Refresh
    </Button>
  );
}
