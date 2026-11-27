"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

import type { ApprovalPageLink } from "./approval-pages";

// Each list is a page of its own, so the links sit in a navigation landmark that marks the page on screen as current, rather
// than in ARIA tabs, which switch panels within one page.
export function ApprovalPageLinks({ links }: { links: readonly ApprovalPageLink[] }) {
  const pathname = usePathname();

  return (
    <nav aria-label="Approvals" className="min-w-0" data-testid="approval-page-links">
      <ul className="flex w-fit max-w-full flex-wrap gap-1 rounded-lg bg-muted p-1">
        {links.map((link) => (
          <li key={link.key} className="flex">
            <Link
              href={link.path}
              aria-current={pathname === link.path ? "page" : undefined}
              className="inline-flex min-h-8 items-center rounded-md border border-transparent px-3 py-1 text-sm font-medium text-muted-foreground focus-ring transition-colors hover:text-foreground aria-[current=page]:bg-background aria-[current=page]:text-foreground aria-[current=page]:shadow-sm dark:aria-[current=page]:border-input"
              data-testid="approval-page-link"
              data-page={link.key}
            >
              {link.title}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
