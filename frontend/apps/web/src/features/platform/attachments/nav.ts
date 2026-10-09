import { PaperclipIcon } from "lucide-react";

import { platformArea } from "../_shared/platform-area";

export const attachmentsNavigation = {
  id: "platform-attachments",
  title: "Attachments",
  description: "Upload, download and delete the files kept with the ERP.",
  basePath: "/platform/attachments",
  featureFlag: "Erp.Modules.Platform.Attachments",
  area: platformArea,
  icon: PaperclipIcon,
} as const;
