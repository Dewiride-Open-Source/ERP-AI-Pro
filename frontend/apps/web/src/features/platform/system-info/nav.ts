import { ServerIcon } from "lucide-react";

import { platformArea } from "../_shared/platform-area";

export const systemInfoNavigation = {
  id: "platform-system-info",
  title: "System",
  description: "See which version of the ERP is running, since when, and its recent starts.",
  basePath: "/platform/system-info",
  featureFlag: "Erp.Modules.Platform.SystemInfo",
  area: platformArea,
  icon: ServerIcon,
} as const;
