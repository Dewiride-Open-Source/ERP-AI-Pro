import "server-only";

import { cache } from "react";

import { callApi } from "@/shared/api/client";

import {
  departmentOptions,
  designationOptions,
  type OrganisationOption,
  personOptions,
} from "../components/organisation-options";

const lookupPageSize = 200;

const searchResultLimit = 20;

interface Page<T> {
  readonly items?: T[] | null;
  readonly totalPages?: number | null;
}

export async function everyPage<T>(read: (page: number) => Promise<Page<T>>): Promise<T[]> {
  const first = await read(1);
  const rest = await Promise.all(
    Array.from({ length: Math.max((first.totalPages ?? 1) - 1, 0) }, (_, index) => read(index + 2)),
  );
  return [first, ...rest].flatMap((page) => page.items ?? []);
}

const departments = cache(() =>
  everyPage((page) =>
    callApi((client) =>
      client.api.identity.users.departments.get({
        queryParameters: { page, pageSize: lookupPageSize, sort: "name:asc" },
      }),
    ),
  ),
);

const designations = cache(() =>
  everyPage((page) =>
    callApi((client) =>
      client.api.identity.users.designations.get({
        queryParameters: { page, pageSize: lookupPageSize, sort: "name:asc" },
      }),
    ),
  ),
);

export async function listDepartmentOptions(): Promise<OrganisationOption[]> {
  const entries = await departments();
  return departmentOptions(
    entries.flatMap((department) =>
      department.id && department.name
        ? [{ id: department.id, name: department.name, parentId: department.parentId ?? undefined }]
        : [],
    ),
  );
}

export async function listDesignationOptions(): Promise<OrganisationOption[]> {
  const entries = await designations();
  return designationOptions(
    entries.flatMap((designation) =>
      designation.id && designation.name ? [{ id: designation.id, name: designation.name }] : [],
    ),
  );
}

export async function searchActivePeople(
  text: string,
  { excluding = [] }: { readonly excluding?: readonly string[] } = {},
): Promise<OrganisationOption[]> {
  const terms = [
    "status:eq:active",
    ...(text.trim() === "" ? [] : [`displayName:contains:${encodeURIComponent(text.trim())}`]),
  ];
  const page = await callApi((client) =>
    client.api.identity.users.get({
      queryParameters: {
        page: 1,
        pageSize: Math.min(searchResultLimit + excluding.length, lookupPageSize),
        sort: "displayName:asc",
        filter: terms.join(";"),
      },
    }),
  );
  return personOptions(
    (page.items ?? []).flatMap((person) =>
      person.id && person.displayName
        ? [{ id: person.id, displayName: person.displayName, workEmail: person.workEmail ?? undefined }]
        : [],
    ),
    excluding,
    searchResultLimit,
  );
}
