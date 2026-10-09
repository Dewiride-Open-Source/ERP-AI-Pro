import type { Route } from "next";

export type NavigationEntry = {
  id: string;
  title: string;
  description: string;
  basePath: Route;
  featureFlag: string;
  permission?: string;
};
