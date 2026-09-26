import type { Route } from "next";

import { attachmentsNavigation } from "@/features/platform/attachments";
import { systemInfoNavigation } from "@/features/platform/system-info";

export type NavigationEntry = {
  id: string;
  title: string;
  basePath: Route;
  featureFlag: string;
  permission?: string;
};

export const navigation: readonly NavigationEntry[] = [systemInfoNavigation, attachmentsNavigation];
