"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

import { referenceListPages } from "../../list-pages/components/reference-list-pages";

// Each list is a page of its own, so the tabs are links in a navigation landmark that mark the page on screen as current,
// rather than ARIA tabs, which switch panels within one page.
export function ReferenceListTabLinks() {
  const pathname = usePathname();

  return (
    <nav aria-label="Reference lists" className="min-w-0" data-testid="reference-lists-tabs">
      <ul className="flex w-fit max-w-full flex-wrap gap-1 rounded-lg bg-muted p-1">
        {referenceListPages.map((page) => (
          <li key={page.path} className="flex">
            <Link
              href={page.path}
              aria-current={pathname === page.path ? "page" : undefined}
              className="inline-flex min-h-8 items-center rounded-md border border-transparent px-3 py-1 text-sm font-medium text-muted-foreground focus-ring transition-colors hover:text-foreground aria-[current=page]:bg-background aria-[current=page]:text-foreground aria-[current=page]:shadow-sm dark:aria-[current=page]:border-input"
            >
              {page.title}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
