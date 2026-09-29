import type { ComponentProps } from "react";

import { cn } from "@dewiride/erp-ui/lib/utils";

export type LoadingStatusProps = Omit<ComponentProps<"div">, "role" | "aria-label"> & {
  label: string;
};

export function LoadingStatus({ label, className, children, ...props }: LoadingStatusProps) {
  return (
    <div {...props} role="status" aria-label={label} data-slot="loading-status" className={cn(className)}>
      <span className="sr-only">{label}</span>
      {children}
    </div>
  );
}
