import type { Permission } from "@dewiride/erp-api-client";
import type { ReactNode } from "react";

import { NoAccessMessage } from "@/shared/layout/no-access-message";

import { mayShow } from "./permissions";
import { requireSession } from "./session";

// A page wraps what it shows in this rather than its segment layout deciding, because a layout cannot keep the page below
// it from rendering or from fetching what it shows.
export async function RequirePermission({
  permission,
  children,
}: {
  permission: Permission;
  children: ReactNode;
}) {
  return mayShow(await requireSession(), permission) ? children : <NoAccessMessage />;
}
