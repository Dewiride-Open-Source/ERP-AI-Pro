import { ListChecksIcon } from "lucide-react";

import { platformArea } from "../_shared/platform-area";

export const approvalsNavigation = {
  id: "platform-approvals",
  title: "Approvals",
  description: "See the requests waiting for your decision and follow the ones you asked for.",
  basePath: "/platform/approvals",
  featureFlag: "Erp.Modules.Platform.Approvals",
  permission: "platform.approvals.requests.read",
  area: platformArea,
  icon: ListChecksIcon,
} as const;
