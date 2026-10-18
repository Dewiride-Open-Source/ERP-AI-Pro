import { useState } from "react";

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

  return {
    isExpanded: (id) => expanded.has(id),
    toggle: (id) => setExpanded((current) => toggledBranch(current, id)),
    expand: (ids) => setExpanded((current) => withBranches(current, ids)),
    expandAll: () => setExpanded(new Set(branchIds(nodes))),
    collapseAll: () => setExpanded(noBranches),
  };
}
