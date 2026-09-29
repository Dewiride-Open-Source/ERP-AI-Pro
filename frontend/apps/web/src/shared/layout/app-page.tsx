import { PageTransition } from "@dewiride/erp-ui/components/motion/page-transition";
import type { ReactNode } from "react";

export function AppPage({ children }: { children: ReactNode }) {
  return <PageTransition>{children}</PageTransition>;
}
