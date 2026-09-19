import "server-only";

import { cache } from "react";

import { apiFetch } from "@/shared/api/http";

export type SystemInfo = {
  applicationName: string;
  version: string;
  startedAt: string;
  uptimeSeconds: number;
};

export const getSystemInfo = cache(() => apiFetch<SystemInfo>("/api/platform/system-info"));
