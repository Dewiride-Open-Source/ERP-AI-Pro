import "server-only";

import { cache } from "react";

import { callApi } from "@/shared/api/client";

export const getRecentStartups = cache(() =>
  callApi((client) => client.api.platform.systemInfo.startups.get()),
);
