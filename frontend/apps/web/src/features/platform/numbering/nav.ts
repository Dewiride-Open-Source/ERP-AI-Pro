import { ListOrderedIcon } from "lucide-react";

import { platformArea } from "../_shared/platform-area";

export const numberingNavigation = {
  id: "platform-numbering",
  title: "Document numbering",
  description: "Set how invoices and other documents are numbered in each financial year.",
  basePath: "/platform/numbering",
  featureFlag: "Erp.Modules.Platform.Numbering",
  permission: "platform.numbering.series.read",
  area: platformArea,
  icon: ListOrderedIcon,
} as const;
