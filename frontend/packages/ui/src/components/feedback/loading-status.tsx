import type { ComponentProps } from "react";

import { cn } from "@dewiride/erp-ui/lib/utils";

export type LoadingStatusProps = Omit<ComponentProps<"div">, "role" | "aria-label"> & {
  label: string;
  pageTitle?: string | undefined;
};

// Next.js announces a navigation by the document title, or by the first h1 while the title is still empty, and the skeleton
// of a page that is still on its way is what is on screen then, so a page's skeleton names the page in a hidden h1.
export function LoadingStatus({ label, pageTitle, className, children, ...props }: LoadingStatusProps) {
  return (
    <div {...props} role="status" aria-label={label} data-slot="loading-status" className={cn(className)}>
      {pageTitle === undefined ? null : <h1 className="sr-only">{pageTitle}</h1>}
      <span className="sr-only">{label}</span>
      {children}
    </div>
  );
}
