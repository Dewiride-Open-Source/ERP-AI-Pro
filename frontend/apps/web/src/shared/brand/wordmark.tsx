import { cn } from "@dewiride/erp-ui/lib/utils";

import { publicEnv } from "@/shared/config/public-env";

export function Wordmark({ className }: { className?: string }) {
  return (
    <span className={cn("inline-flex items-center gap-2 font-semibold tracking-tight", className)}>
      <span
        aria-hidden
        className="inline-flex size-7 items-center justify-center rounded-lg bg-primary text-sm font-bold text-primary-foreground shadow-sm"
      >
        D
      </span>
      <span>{publicEnv.appName}</span>
    </span>
  );
}
