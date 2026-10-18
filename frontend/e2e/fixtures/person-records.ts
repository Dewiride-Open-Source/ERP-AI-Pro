import { expect, type APIResponse } from "@playwright/test";
import { randomUUID } from "node:crypto";

import type { SignedInPerson } from "./sign-in";
import type { SignedInApi } from "./test";

export const peopleApi = "/api/identity/users";

export const departmentsApi = "/api/identity/users/departments";

export const designationsApi = "/api/identity/users/designations";

export type PersonRole = "finance" | "hr" | "manager";

export type PersonDetails = {
  readonly displayName: string;
  readonly workEmail: string;
  readonly employeeCode?: string;
  readonly phoneNumber?: string;
  readonly dateOfJoining?: string;
  readonly roles?: readonly PersonRole[];
};

export type PersonRecord = {
  readonly id: string;
  readonly displayName: string;
  readonly workEmail: string;
  readonly employeeCode: string | null;
  readonly phoneNumber: string | null;
  readonly dateOfJoining: string | null;
  readonly roles: readonly PersonRole[];
  readonly status: "active" | "deactivated";
  readonly version: string;
};

export type NamedRecord = { readonly id: string; readonly name: string };

export type MoveDetails = {
  readonly effectiveFrom: string;
  readonly departmentId?: string;
  readonly designationId?: string;
  readonly managerId?: string;
};

export type Records = {
  readonly people?: readonly string[];
  readonly departments?: readonly string[];
  readonly designations?: readonly string[];
};

// Other workers create people, departments and designations at the same time, so every record a test makes carries a
// prefix of its own, which its assertions and searches match.
export function uniquePrefix(feature: string): string {
  return `e2e-${feature}-${randomUUID().slice(-12)}`;
}

export async function registerPersonRecord(api: SignedInApi, details: PersonDetails): Promise<PersonRecord> {
  const response = await api.post(peopleApi, {
    headers: { "Idempotency-Key": randomUUID() },
    data: details,
  });
  expect(response.status(), `the registration of ${details.displayName}`).toBe(201);
  return (await response.json()) as PersonRecord;
}

export async function personRecord(api: SignedInApi, id: string): Promise<PersonRecord> {
  const response = await api.get(`${peopleApi}/${id}`);
  expect(response.status(), `the record ${id}`).toBe(200);
  return (await response.json()) as PersonRecord;
}

// Every sign-in of a persona is a new account with the persona's work email, so the record that sign-in admitted is the one
// with that email linked to the account's object id, among the newest records.
export async function ownPersonRecordId(
  api: { readonly get: (path: string) => Promise<APIResponse> },
  signedIn: SignedInPerson | undefined,
): Promise<string> {
  expect(signedIn, "the signed-in persona").toBeDefined();
  const query = new URLSearchParams({
    filter: `workEmail:eq:${encodeURIComponent((signedIn?.userName ?? "").toLowerCase())}`,
    sort: "createdAt:desc",
    pageSize: "100",
  });
  const response = await api.get(`${peopleApi}?${query.toString()}`);
  expect(response.status(), "the records with the persona's work email").toBe(200);
  const { items } = (await response.json()) as {
    items?: readonly { readonly id?: string; readonly entraObjectId?: string | null }[];
  };
  const own = items?.find((record) => record.entraObjectId?.toLowerCase() === signedIn?.id.toLowerCase());
  expect(own?.id, "the record the persona's sign-in admitted").toBeDefined();
  return own?.id ?? "";
}

export async function deactivatePersonRecord(
  api: SignedInApi,
  person: { readonly id: string; readonly version: string },
): Promise<void> {
  const response = await api.put(`${peopleApi}/${person.id}/status`, {
    data: { status: "deactivated", version: person.version },
  });
  expect(response.status(), `the deactivation of ${person.id}`).toBe(200);
}

export async function createDepartmentRecord(
  api: SignedInApi,
  name: string,
  parentId?: string,
): Promise<NamedRecord> {
  return created(
    await api.post(departmentsApi, {
      headers: { "Idempotency-Key": randomUUID() },
      data: parentId === undefined ? { name } : { name, parentId },
    }),
    name,
  );
}

export async function createDesignationRecord(api: SignedInApi, name: string): Promise<NamedRecord> {
  return created(
    await api.post(designationsApi, { headers: { "Idempotency-Key": randomUUID() }, data: { name } }),
    name,
  );
}

export async function recordMoveRecord(
  api: SignedInApi,
  personId: string,
  move: MoveDetails,
): Promise<string> {
  const response = await api.post(`${peopleApi}/${personId}/positions`, {
    headers: { "Idempotency-Key": randomUUID() },
    data: move,
  });
  expect(response.status(), `the move of ${personId} from ${move.effectiveFrom}`).toBe(201);
  const { id } = (await response.json()) as { id?: unknown };
  expect(typeof id, "the id of the move").toBe("string");
  return id as string;
}

export async function movesOf(
  api: SignedInApi,
  personId: string,
): Promise<readonly { effectiveFrom: string }[]> {
  const response = await api.get(`${peopleApi}/${personId}/positions?pageSize=200`);
  expect(response.status(), `the moves of ${personId}`).toBe(200);
  const { items } = (await response.json()) as { items?: readonly { effectiveFrom: string }[] };
  return items ?? [];
}

// A person someone reports to keeps their record until those people are gone, and a department or designation stays while
// a move of a remaining person names it, so the people go first, in the order given, reports before their managers.
export async function removeRecords(api: SignedInApi, records: Records): Promise<void> {
  for (const id of records.people ?? []) removed(await api.delete(`${peopleApi}/${id}`), id);
  for (const id of records.departments ?? []) removed(await api.delete(`${departmentsApi}/${id}`), id);
  for (const id of records.designations ?? []) removed(await api.delete(`${designationsApi}/${id}`), id);
}

async function created(response: APIResponse, name: string): Promise<NamedRecord> {
  expect(response.status(), `the creation of ${name}`).toBe(201);
  const { id } = (await response.json()) as { id?: unknown };
  expect(typeof id, `the id of ${name}`).toBe("string");
  return { id: id as string, name };
}

function removed(response: APIResponse, id: string): void {
  expect([204, 404], `the removal of ${id}`).toContain(response.status());
}
