import "server-only";

import { cache } from "react";

import { apiFetch } from "@/shared/api/http";

import type { FeatureFlags } from "./feature-flags";

type FeaturesResponse = {
  features: { name: string; enabled: boolean }[];
};

export const getFeatureFlags = cache(async (): Promise<FeatureFlags> => {
  try {
    const response = await apiFetch<FeaturesResponse>("/platform/features");
    return new Map(response.features.map((feature) => [feature.name, feature.enabled]));
  } catch {
    return new Map();
  }
});
