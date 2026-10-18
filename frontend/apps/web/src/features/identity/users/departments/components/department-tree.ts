import { byName } from "../../organisation/components/disclosure-tree.ts";
import {
  departmentOptions,
  type OrganisationOption,
} from "../../organisation/components/organisation-options.ts";

export interface DepartmentRecord {
  readonly id: string;
  readonly name: string;
  readonly parentId: string | undefined;
  readonly version: string | undefined;
}

export interface DepartmentNode extends DepartmentRecord {
  readonly children: readonly DepartmentNode[];
}

export interface DepartmentResponseLike {
  readonly id?: string | null | undefined;
  readonly name?: string | null | undefined;
  readonly parentId?: string | null | undefined;
  readonly version?: string | null | undefined;
}

export function departmentRecords(departments: readonly DepartmentResponseLike[]): DepartmentRecord[] {
  return departments.flatMap((department) =>
    department.id && department.name
      ? [
          {
            id: department.id,
            name: department.name,
            parentId: department.parentId ?? undefined,
            version: department.version ?? undefined,
          },
        ]
      : [],
  );
}

// The API keeps the tree free of cycles, but a list read page by page while someone moves departments can name a parent it
// does not hold or close a loop, so such a department sits at the top rather than vanishing or repeating.
export function departmentTree(records: readonly DepartmentRecord[]): readonly DepartmentNode[] {
  const known = new Set(records.map((record) => record.id));
  const childrenOf = new Map<string, DepartmentRecord[]>();
  const tops: DepartmentRecord[] = [];
  for (const record of records) {
    const { parentId } = record;
    if (parentId === undefined || parentId === record.id || !known.has(parentId)) tops.push(record);
    else childrenOf.set(parentId, [...(childrenOf.get(parentId) ?? []), record]);
  }

  const placed = new Set<string>();
  const build = (record: DepartmentRecord): DepartmentNode => {
    placed.add(record.id);
    const children = [...(childrenOf.get(record.id) ?? [])]
      .sort(byName)
      .filter((child) => !placed.has(child.id))
      .map(build);
    return { ...record, children };
  };

  const nodes = [...tops].sort(byName).map(build);
  for (const record of [...records].sort(byName)) {
    if (!placed.has(record.id)) nodes.push(build(record));
  }
  return nodes.sort(byName);
}

export function departmentsWithin(
  records: readonly DepartmentRecord[],
  departmentId: string,
): ReadonlySet<string> {
  const within = new Set([departmentId]);
  let grew = true;
  while (grew) {
    grew = false;
    for (const record of records) {
      if (!within.has(record.id) && record.parentId !== undefined && within.has(record.parentId)) {
        within.add(record.id);
        grew = true;
      }
    }
  }
  return within;
}

// A department can go under any department except itself and those within it, which the API refuses.
export function parentChoices(
  records: readonly DepartmentRecord[],
  departmentId: string | undefined,
): readonly OrganisationOption[] {
  if (departmentId === undefined) return departmentOptions(records);
  const within = departmentsWithin(records, departmentId);
  return departmentOptions(records.filter((record) => !within.has(record.id)));
}

export function ancestorsOf(records: readonly DepartmentRecord[], departmentId: string): readonly string[] {
  const byId = new Map(records.map((record) => [record.id, record]));
  const ancestors: string[] = [];
  const visited = new Set([departmentId]);
  let parentId = byId.get(departmentId)?.parentId;
  while (parentId !== undefined && !visited.has(parentId) && byId.has(parentId)) {
    visited.add(parentId);
    ancestors.push(parentId);
    parentId = byId.get(parentId)?.parentId;
  }
  return ancestors;
}
