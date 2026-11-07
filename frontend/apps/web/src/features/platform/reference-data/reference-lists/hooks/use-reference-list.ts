import { use } from "react";

import { ReferenceListsContext } from "../components/reference-lists-context";
import type { ReferenceLists } from "../components/reference-options";

export function useReferenceList<K extends keyof ReferenceLists>(kind: K): NonNullable<ReferenceLists[K]> {
  const list = use(ReferenceListsContext)?.[kind];
  if (list === undefined) {
    throw new Error(`A picker of ${kind} needs a ReferenceListsProvider around it that reads ${kind}.`);
  }
  return list as NonNullable<ReferenceLists[K]>;
}
