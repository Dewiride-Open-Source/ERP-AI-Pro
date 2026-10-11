import type { Permission } from "@dewiride/erp-api-client";
import type { Metadata } from "next";
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

// A page names a refusal in its title as well, because the router announces the new title after a client navigation, so a
// person who follows a link they may not use hears that they have no access rather than the name of the page they asked for.
export async function pageMetadata(title: string, permission: Permission): Promise<Metadata> {
  return { title: mayShow(await requireSession(), permission) ? title : "No access" };
}
