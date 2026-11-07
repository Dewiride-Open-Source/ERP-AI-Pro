import { use } from "react";

import { ReferenceListsContext, type ReferenceListsValue } from "../components/reference-lists-context";
import type { ReferenceLists } from "../components/reference-options";

function useReferenceLists(): ReferenceListsValue {
  const value = use(ReferenceListsContext);
  if (value === null) throw new Error("A reference picker needs a ReferenceListsProvider around it.");
  return value;
}

export function useReferenceList<K extends keyof ReferenceLists>(kind: K): NonNullable<ReferenceLists[K]> {
  const list = useReferenceLists().lists[kind];
  if (list === undefined) {
    throw new Error(`A picker of ${kind} needs a ReferenceListsProvider around it that reads ${kind}.`);
  }
  return list as NonNullable<ReferenceLists[K]>;
}

export function useReferenceToday(): string {
  return useReferenceLists().today;
}
