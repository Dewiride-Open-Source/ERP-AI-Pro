import { useCallback, useState } from "react";

import { branchIds, toggledBranch, withBranches, type TreeBranch } from "../components/disclosure-tree";

export interface DisclosureTree {
  readonly isExpanded: (id: string) => boolean;
  readonly toggle: (id: string) => void;
  readonly expand: (ids: readonly string[]) => void;
  readonly expandAll: () => void;
  readonly collapseAll: () => void;
}

const noBranches: ReadonlySet<string> = new Set();

export function useDisclosureTree(
  nodes: readonly TreeBranch[],
  initiallyExpanded: readonly string[] = [],
): DisclosureTree {
  const [expanded, setExpanded] = useState<ReadonlySet<string>>(() => new Set(initiallyExpanded));
  const toggle = useCallback((id: string) => setExpanded((current) => toggledBranch(current, id)), []);
  const expand = useCallback(
    (ids: readonly string[]) => setExpanded((current) => withBranches(current, ids)),
    [],
  );
  const expandAll = useCallback(() => setExpanded(new Set(branchIds(nodes))), [nodes]);
  const collapseAll = useCallback(() => setExpanded(noBranches), []);

  return { isExpanded: (id) => expanded.has(id), toggle, expand, expandAll, collapseAll };
}
