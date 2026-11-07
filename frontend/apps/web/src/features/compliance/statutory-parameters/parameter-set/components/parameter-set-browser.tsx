"use client";

import type { ReactNode } from "react";

import { ListNavigationProvider, useListNavigation } from "@/shared/lists/list-navigation";

import { VersionList, type VersionListProps } from "./version-list";

// The versions, the entries and the changes share one navigation, so while a version or a page of entries is on its way
// every card that still shows the version before says so, and the entries' searched list joins it (outerNavigation).
export function ParameterSetBrowser({
  versions,
  entries,
  changes,
}: {
  versions: VersionListProps;
  entries: ReactNode;
  changes: ReactNode;
}) {
  return (
    <ListNavigationProvider>
      <VersionList {...versions} />
      {entries}
      {changes === null ? null : <PendingRegion>{changes}</PendingRegion>}
    </ListNavigationProvider>
  );
}

function PendingRegion({ children }: { children: ReactNode }) {
  const { pending } = useListNavigation();

  return (
    <div
      aria-busy={pending || undefined}
      data-pending={pending || undefined}
      className="min-w-0 transition-opacity duration-(--motion-duration-fast) data-pending:opacity-60"
    >
      {children}
    </div>
  );
}
