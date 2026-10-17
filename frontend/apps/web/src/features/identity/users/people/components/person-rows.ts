import type { PersonResponse, PersonRole, PersonStatus } from "@dewiride/erp-api-client";

import { isoDateOf } from "@/shared/api/dates/date-only";

import type { OrganisationOption } from "../../organisation/components/organisation-options.ts";

// A row crosses from the server to the browser, so it holds plain values only: a date of the API is written as yyyy-MM-dd.
export interface PersonRow {
  readonly id: string;
  readonly displayName: string;
  readonly workEmail: string | undefined;
  readonly employeeCode: string | undefined;
  readonly departmentName: string | undefined;
  readonly designationName: string | undefined;
  readonly managerDisplayName: string | undefined;
  readonly dateOfJoining: string | undefined;
  readonly status: PersonStatus | undefined;
  readonly roles: readonly PersonRole[];
  readonly lastSignedInAt: Date | undefined;
}

export function personRows(people: readonly PersonResponse[]): PersonRow[] {
  return people.flatMap((person) =>
    person.id && person.displayName
      ? [
          {
            id: person.id,
            displayName: person.displayName,
            workEmail: person.workEmail ?? undefined,
            employeeCode: person.employeeCode ?? undefined,
            departmentName: person.departmentName ?? undefined,
            designationName: person.designationName ?? undefined,
            managerDisplayName: person.managerDisplayName ?? undefined,
            dateOfJoining: isoDateOf(person.dateOfJoining),
            status: person.status ?? undefined,
            roles: person.roles ?? [],
            lastSignedInAt: person.lastSignedInAt ?? undefined,
          },
        ]
      : [],
  );
}

// A department or designation the address names but the lookup does not list, deleted since or never there, stays a choice
// the filter shows, so the person sees what the list is filtered by and can clear it.
export function filterOptions(
  options: readonly OrganisationOption[] | undefined,
  chosen: readonly string[],
  unknownLabel: string,
): OrganisationOption[] {
  const listed = options ?? [];
  const known = new Set(listed.map((option) => option.value));
  return [
    ...listed,
    ...chosen.filter((value) => !known.has(value)).map((value) => ({ value, label: unknownLabel })),
  ];
}
