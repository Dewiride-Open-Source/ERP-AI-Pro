import type { Route } from "next";

export type NavigationEntry = {
  id: string;
  title: string;
  basePath: Route;
  featureFlag: string;
  permission?: string;
};
