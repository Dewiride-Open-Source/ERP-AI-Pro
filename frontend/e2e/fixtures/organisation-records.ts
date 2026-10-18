import { expect } from "@playwright/test";

import { departmentsApi, designationsApi } from "./person-records";
import type { SignedInApi } from "./test";

type Listed = { readonly id: string; readonly name: string; readonly parentId?: string | null };

async function namedRecords(api: SignedInApi, path: string, prefix: string): Promise<readonly Listed[]> {
  const query = new URLSearchParams({
    filter: `name:contains:${encodeURIComponent(prefix)}`,
    pageSize: "200",
  });
  const response = await api.get(`${path}?${query.toString()}`);
  expect(response.status(), `the records named ${prefix}`).toBe(200);
  const { items } = (await response.json()) as { items?: readonly Listed[] };
  return items ?? [];
}

// The pages make records whose ids a test never sees, so the clean-up finds them again by the test's prefix; a department
// goes before the one it sits under, because the API keeps a department that still holds others.
export async function departmentIdsNamed(api: SignedInApi, prefix: string): Promise<string[]> {
  const departments = await namedRecords(api, departmentsApi, prefix);
  const byId = new Map(departments.map((department) => [department.id, department]));
  const depth = (department: Listed): number => {
    let levels = 0;
    const seen = new Set([department.id]);
    let parentId = department.parentId ?? undefined;
    while (parentId !== undefined && !seen.has(parentId)) {
      seen.add(parentId);
      levels += 1;
      parentId = byId.get(parentId)?.parentId ?? undefined;
    }
    return levels;
  };
  return [...departments]
    .sort((left, right) => depth(right) - depth(left))
    .map((department) => department.id);
}

export async function designationIdsNamed(api: SignedInApi, prefix: string): Promise<string[]> {
  return (await namedRecords(api, designationsApi, prefix)).map((designation) => designation.id);
}
