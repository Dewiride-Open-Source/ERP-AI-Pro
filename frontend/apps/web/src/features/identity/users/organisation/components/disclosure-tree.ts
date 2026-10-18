export interface TreeBranch {
  readonly id: string;
  readonly children: readonly TreeBranch[];
}

const collator = new Intl.Collator("en-IN", { sensitivity: "base", numeric: true });

const counts = new Intl.NumberFormat("en-IN");

// Names that differ only in case or accents sort together, so the id breaks the tie and every render orders them alike.
export function byName(
  left: { readonly id: string; readonly name: string },
  right: { readonly id: string; readonly name: string },
): number {
  const byText = collator.compare(left.name, right.name);
  if (byText !== 0) return byText;
  return left.id < right.id ? -1 : left.id > right.id ? 1 : 0;
}

export function branchIds(nodes: readonly TreeBranch[]): readonly string[] {
  const ids: string[] = [];
  const visit = (node: TreeBranch): void => {
    if (node.children.length === 0) return;
    ids.push(node.id);
    node.children.forEach(visit);
  };
  nodes.forEach(visit);
  return ids;
}

export function toggledBranch(expanded: ReadonlySet<string>, id: string): ReadonlySet<string> {
  const next = new Set(expanded);
  if (next.has(id)) next.delete(id);
  else next.add(id);
  return next;
}

export function withBranches(expanded: ReadonlySet<string>, ids: readonly string[]): ReadonlySet<string> {
  return ids.every((id) => expanded.has(id)) ? expanded : new Set([...expanded, ...ids]);
}

export function countLabel(count: number, one: string, many: string): string {
  return count === 1 ? `1 ${one}` : `${counts.format(count)} ${many}`;
}
