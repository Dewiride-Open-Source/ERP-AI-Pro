import type { ReactNode } from "react";

import { ApprovalPagesFrame } from "@/features/platform/approvals";

export default function ApprovalListsLayout({ children }: Readonly<{ children: ReactNode }>) {
  return <ApprovalPagesFrame>{children}</ApprovalPagesFrame>;
}
