import "server-only";

import { cache } from "react";

import { apiFetch } from "@/shared/api/http";

export type Startup = {
  id: string;
  applicationName: string;
  version: string;
  framework: string;
  environmentName: string;
  configurationLabel: string | null;
  startedAt: string;
  recordedAt: string;
};

export type RecentStartups = { startups: Startup[] };

export const getRecentStartups = cache(() => apiFetch<RecentStartups>("/platform/system-info/startups"));
