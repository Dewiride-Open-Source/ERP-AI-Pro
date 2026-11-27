import type { ReactNode } from "react";

import { mayShow } from "@/shared/auth/permissions";
import { requireSession } from "@/shared/auth/session";

import { approvalsNavigation } from "../../nav";

import { ApprovalPageLinks } from "./approval-page-links";
import { approvalPageLinks } from "./approval-pages";

// A person refused the requests sees the page's no-access message alone, without links that lead to more refusals.
export async function ApprovalPagesFrame({ children }: { children: ReactNode }) {
  const session = await requireSession();
  if (!mayShow(session, approvalsNavigation.permission)) return children;

  return (
    <div className="grid min-w-0 gap-section">
      <ApprovalPageLinks
        links={approvalPageLinks(mayShow(session, "platform.approvals.requests.read-all"))}
      />
      {children}
    </div>
  );
}
