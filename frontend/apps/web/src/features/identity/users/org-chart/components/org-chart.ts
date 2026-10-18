import { byName } from "../../organisation/components/disclosure-tree.ts";

export interface ChartPerson {
  readonly id: string;
  readonly name: string;
  readonly departmentId: string | undefined;
  readonly departmentName: string | undefined;
  readonly designationName: string | undefined;
  readonly managerId: string | undefined;
  readonly managerName: string | undefined;
}

export type ManagerOutside = "off-chart" | "other-department";

export interface ChartNode extends ChartPerson {
  readonly managerOutside: ManagerOutside | undefined;
  readonly children: readonly ChartNode[];
}

export interface ChartPersonResponseLike {
  readonly id?: string | null | undefined;
  readonly displayName?: string | null | undefined;
  readonly departmentId?: string | null | undefined;
  readonly departmentName?: string | null | undefined;
  readonly designationName?: string | null | undefined;
  readonly managerId?: string | null | undefined;
  readonly managerDisplayName?: string | null | undefined;
}

export interface ChartPage {
  readonly roots: readonly ChartNode[];
  readonly page: number;
  readonly pageCount: number;
}

export const peopleAtTheTopPerPage = 20;

export function chartPeople(entries: readonly ChartPersonResponseLike[]): ChartPerson[] {
  return entries.flatMap((entry) =>
    entry.id && entry.displayName
      ? [
          {
            id: entry.id,
            name: entry.displayName,
            departmentId: entry.departmentId ?? undefined,
            departmentName: entry.departmentName ?? undefined,
            designationName: entry.designationName ?? undefined,
            managerId: entry.managerId ?? undefined,
            managerName: entry.managerDisplayName ?? undefined,
          },
        ]
      : [],
  );
}

// Everyone shown sits under their manager when the manager is shown too, and at the top otherwise, where the reason is
// kept: a manager missing from the whole chart is deactivated or deleted, or has not joined by the date, while one on the
// chart but outside the chosen departments works elsewhere. The API refuses reporting cycles; should a list read page by
// page meet one, its people still appear once each, from the first name.
export function orgChart(
  people: readonly ChartPerson[],
  { departments }: { readonly departments?: ReadonlySet<string> | undefined } = {},
): readonly ChartNode[] {
  const onChart = new Set(people.map((person) => person.id));
  const shown =
    departments === undefined
      ? people
      : people.filter((person) => person.departmentId !== undefined && departments.has(person.departmentId));
  const shownIds = new Set(shown.map((person) => person.id));
  const reportsOf = new Map<string, ChartPerson[]>();
  const tops: ChartPerson[] = [];
  for (const person of shown) {
    const { managerId } = person;
    if (managerId !== undefined && managerId !== person.id && shownIds.has(managerId)) {
      reportsOf.set(managerId, [...(reportsOf.get(managerId) ?? []), person]);
    } else {
      tops.push(person);
    }
  }

  const placed = new Set<string>();
  const build = (person: ChartPerson, managerOutside: ManagerOutside | undefined): ChartNode => {
    placed.add(person.id);
    const children = [...(reportsOf.get(person.id) ?? [])]
      .sort(byName)
      .filter((report) => !placed.has(report.id))
      .map((report) => build(report, undefined));
    return { ...person, managerOutside, children };
  };
  const outside = (person: ChartPerson): ManagerOutside | undefined => {
    if (person.managerId === undefined || person.managerId === person.id) return undefined;
    return onChart.has(person.managerId) ? "other-department" : "off-chart";
  };

  const roots = [...tops].sort(byName).map((person) => build(person, outside(person)));
  for (const person of [...shown].sort(byName)) {
    if (!placed.has(person.id)) roots.push(build(person, undefined));
  }
  return roots.sort(byName);
}

export function chartPage(roots: readonly ChartNode[], page: number): ChartPage {
  const pageCount = Math.max(1, Math.ceil(roots.length / peopleAtTheTopPerPage));
  const shownPage = Math.min(Math.max(1, page), pageCount);
  const start = (shownPage - 1) * peopleAtTheTopPerPage;
  return { roots: roots.slice(start, start + peopleAtTheTopPerPage), page: shownPage, pageCount };
}

// A large chart opens with the people at the top showing whom they manage directly, and everyone further down folded.
export function initiallyExpanded(roots: readonly ChartNode[]): readonly string[] {
  return roots.filter((root) => root.children.length > 0).map((root) => root.id);
}

export function chartPeopleCount(roots: readonly ChartNode[]): number {
  return roots.reduce((count, root) => count + 1 + chartPeopleCount(root.children), 0);
}
