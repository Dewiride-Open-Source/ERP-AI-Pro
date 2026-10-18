export interface OrganisationOption {
  readonly value: string;
  readonly label: string;
  readonly description?: string | undefined;
}

export interface DepartmentEntry {
  readonly id: string;
  readonly name: string;
  readonly parentId: string | undefined;
}

export interface DesignationEntry {
  readonly id: string;
  readonly name: string;
}

export interface PersonEntry {
  readonly id: string;
  readonly displayName: string;
  readonly workEmail: string | undefined;
}

const collator = new Intl.Collator("en-IN", { sensitivity: "base", numeric: true });

const pathSeparator = " › ";

function byLabel(left: OrganisationOption, right: OrganisationOption): number {
  return collator.compare(left.label, right.label);
}

export function departmentPath(
  department: DepartmentEntry,
  departmentsById: ReadonlyMap<string, DepartmentEntry>,
): string {
  const names: string[] = [];
  const visited = new Set<string>();
  let current: DepartmentEntry | undefined = department;
  while (current !== undefined && !visited.has(current.id)) {
    visited.add(current.id);
    names.unshift(current.name);
    current = current.parentId === undefined ? undefined : departmentsById.get(current.parentId);
  }
  return names.join(pathSeparator);
}

export function departmentOptions(departments: readonly DepartmentEntry[]): OrganisationOption[] {
  const departmentsById = new Map(departments.map((department) => [department.id, department]));
  return departments
    .map((department) => ({ value: department.id, label: departmentPath(department, departmentsById) }))
    .sort(byLabel);
}

export function designationOptions(designations: readonly DesignationEntry[]): OrganisationOption[] {
  return designations
    .map((designation) => ({ value: designation.id, label: designation.name }))
    .sort(byLabel);
}

export function personOptions(
  people: readonly PersonEntry[],
  excludedPersonIds: readonly string[] = [],
): OrganisationOption[] {
  return people
    .filter((person) => !excludedPersonIds.includes(person.id))
    .map((person) => ({ value: person.id, label: person.displayName, description: person.workEmail }));
}
