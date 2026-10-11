import type { Permission } from "@dewiride/erp-api-client";

import type { HeldSession, SignedInPerson } from "./session.ts";

export function holdsPermission(
  person: Pick<SignedInPerson, "permissions">,
  permission: Permission,
): boolean {
  return person.permissions.includes(permission);
}

// A render whose session could not be read shows what the permission guards, as one whose feature flags could not be read
// shows every module: the API still refuses each read and change without the permission, and the page shows that failure
// rather than telling a person who may well hold the permission that they lack it.
export function mayShow(session: HeldSession, permission: Permission): boolean {
  return session.status === "unavailable" || holdsPermission(session.person, permission);
}
