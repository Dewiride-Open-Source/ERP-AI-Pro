import type { Permission } from "@dewiride/erp-api-client";
import type { LucideIcon } from "lucide-react";
import type { Route } from "next";

export type NavigationArea = {
  id: string;
  title: string;
};

export type NavigationEntry = {
  id: string;
  title: string;
  description: string;
  basePath: Route;
  featureFlag: string;
  permission: Permission;
  area: NavigationArea;
  icon: LucideIcon;
};
