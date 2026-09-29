import type { ReactNode } from "react";

export function PageTransition({ children }: { children: ReactNode }) {
  return (
    <div data-slot="page-transition" className="animate-page-enter">
      {children}
    </div>
  );
}
