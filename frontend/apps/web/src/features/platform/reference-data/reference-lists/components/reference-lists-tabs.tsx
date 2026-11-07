import type { ReactNode } from "react";

import { mayShow } from "@/shared/auth/permissions";
import { requireSession } from "@/shared/auth/session";

import { referenceListsNavigation } from "../../nav";

import { ReferenceListTabLinks } from "./reference-list-tab-links";

// A person refused the lists sees the page's no-access message alone, without tabs that lead to more refusals.
export async function ReferenceListsTabs({ children }: { children: ReactNode }) {
  if (!mayShow(await requireSession(), referenceListsNavigation.permission)) return children;

  return (
    <div className="grid min-w-0 gap-section">
      <ReferenceListTabLinks />
      {children}
    </div>
  );
}
