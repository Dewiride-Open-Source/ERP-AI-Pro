"use client";

import { PageTransition } from "@dewiride/erp-ui/components/motion/page-transition";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";

// The template stays mounted while the pages under one segment change, so the page's path is what tells one page from the
// next; a change of query, such as a list page, keeps the same page and runs no transition.
export function AppPage({ children }: { children: ReactNode }) {
  return <PageTransition transitionKey={usePathname()}>{children}</PageTransition>;
}
