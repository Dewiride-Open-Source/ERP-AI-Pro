"use client";

import type { DataTableLinkProps } from "@dewiride/erp-ui/components/data-table/data-table";
import type { Route } from "next";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { createContext, use, useMemo, useTransition, type ReactNode } from "react";

interface ListNavigation {
  readonly pending: boolean;
  readonly navigate: (href: Route) => void;
}

const ListNavigationContext = createContext<ListNavigation | null>(null);

export function ListNavigationProvider({ children }: { children: ReactNode }) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const navigation = useMemo<ListNavigation>(
    () => ({
      pending,
      navigate: (href) => startTransition(() => router.push(href, { scroll: false })),
    }),
    [pending, router],
  );

  return <ListNavigationContext value={navigation}>{children}</ListNavigationContext>;
}

export function useListNavigation(): ListNavigation {
  const navigation = use(ListNavigationContext);
  if (navigation === null) throw new Error("useListNavigation needs a ListNavigationProvider above it.");
  return navigation;
}

// A page link keeps its element and focus when it becomes unavailable on the first or last page, so it stays a link
// with aria-disabled instead of turning into a disabled button; the DataTable hands back an href built by listLink.
export function ListLink({
  href,
  disabled,
  children,
  className,
  "aria-label": label,
  onFollow,
}: DataTableLinkProps & { onFollow?: (() => void) | undefined }) {
  const { navigate } = useListNavigation();
  const route = href as Route;

  return (
    <Link
      href={route}
      prefetch={false}
      scroll={false}
      aria-disabled={disabled || undefined}
      aria-label={label}
      className={className}
      onNavigate={(event) => {
        event.preventDefault();
        if (disabled) return;
        onFollow?.();
        navigate(route);
      }}
    >
      {children}
    </Link>
  );
}
