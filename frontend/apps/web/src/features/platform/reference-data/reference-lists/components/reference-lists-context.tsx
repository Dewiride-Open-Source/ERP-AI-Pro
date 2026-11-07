"use client";

import { createContext, type ReactNode } from "react";

import type { ReferenceLists } from "./reference-options";

export const ReferenceListsContext = createContext<ReferenceLists | null>(null);

export function ReferenceListsContextProvider({
  lists,
  children,
}: Readonly<{ lists: ReferenceLists; children: ReactNode }>) {
  return <ReferenceListsContext value={lists}>{children}</ReferenceListsContext>;
}
