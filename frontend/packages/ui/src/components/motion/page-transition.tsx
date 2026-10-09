import { ViewTransition, type ReactNode } from "react";

// A new key mounts a new boundary, so each navigation runs the old page's exit and the new page's enter in one view
// transition, while an update in place (a refresh, a list page, a Suspense reveal) animates nothing.
export function PageTransition({ transitionKey, children }: { transitionKey: string; children: ReactNode }) {
  return (
    <ViewTransition key={transitionKey} enter="page-enter" exit="page-exit" default="none">
      {children}
    </ViewTransition>
  );
}
