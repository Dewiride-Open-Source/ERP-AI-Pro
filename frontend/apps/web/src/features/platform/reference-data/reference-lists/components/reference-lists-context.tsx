"use client";

import { createContext, useMemo, type ReactNode } from "react";

import type { ReferenceLists } from "./reference-options";

export interface ReferenceListsValue {
  readonly lists: ReferenceLists;
  readonly today: string;
}

export const ReferenceListsContext = createContext<ReferenceListsValue | null>(null);

export function ReferenceListsContextProvider({
  lists,
  today,
  children,
}: Readonly<{ lists: ReferenceLists; today: string; children: ReactNode }>) {
  const value = useMemo(() => ({ lists, today }), [lists, today]);

  return <ReferenceListsContext value={value}>{children}</ReferenceListsContext>;
}
