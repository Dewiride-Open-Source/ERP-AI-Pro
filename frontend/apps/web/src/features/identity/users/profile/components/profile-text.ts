import type { ErpRole } from "@dewiride/erp-api-client";

import { roleLabel } from "../../people/components/person-text.ts";

export const profileTitle = "My profile";

export const notRecorded = "Not recorded";

export function recordedText(value: string | null | undefined): string {
  const text = value?.trim();
  return text === undefined || text === "" ? notRecorded : text;
}

export function roleNames(roles: readonly ErpRole[] | null | undefined): readonly string[] {
  return (roles ?? []).map(roleLabel);
}
