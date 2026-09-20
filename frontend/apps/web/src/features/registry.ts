import type { Route } from "next";

import { systemInfoNavigation } from "@/features/platform/system-info";

export type NavigationEntry = {
  id: string;
  title: string;
  basePath: Route;
  featureFlag: string;
  permission?: string;
};

export const navigation: readonly NavigationEntry[] = [systemInfoNavigation];
