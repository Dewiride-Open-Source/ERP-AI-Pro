import "server-only";

import { cache } from "react";

import { callApi } from "@/shared/api/client";

import type { FeatureFlags } from "./feature-flags";

export const getFeatureFlags = cache(async (): Promise<FeatureFlags> => {
  try {
    const response = await callApi((client) => client.api.platform.features.get());
    return new Map(
      (response.features ?? []).flatMap(({ name, enabled }) =>
        name && typeof enabled === "boolean" ? [[name, enabled] as const] : [],
      ),
    );
  } catch {
    return new Map();
  }
});
