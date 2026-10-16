import { HistoryIcon } from "lucide-react";

import { platformArea } from "../_shared/platform-area";

export const auditNavigation = {
  id: "platform-audit",
  title: "Audit trail",
  description: "See who changed what in the ERP, and when.",
  basePath: "/platform/audit",
  featureFlag: "Erp.Modules.Platform.Audit",
  permission: "platform.audit.changes.read",
  area: platformArea,
  icon: HistoryIcon,
} as const;
