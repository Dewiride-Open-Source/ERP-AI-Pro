import { CalendarClockIcon } from "lucide-react";

import { platformArea } from "../_shared/platform-area";

export const jobsNavigation = {
  id: "platform-jobs",
  title: "Background jobs",
  description: "See the work the ERP does on its own, when it runs next, and how each run went.",
  basePath: "/platform/jobs",
  featureFlag: "Erp.Modules.Platform.Jobs",
  permission: "platform.jobs.runs.read",
  area: platformArea,
  icon: CalendarClockIcon,
} as const;
