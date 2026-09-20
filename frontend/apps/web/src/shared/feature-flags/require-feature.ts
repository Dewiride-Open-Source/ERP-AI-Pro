import "server-only";

import { notFound } from "next/navigation";

import { isFeatureEnabled } from "./feature-flags";
import { getFeatureFlags } from "./queries";

export async function requireFeature(name: string): Promise<void> {
  if (!isFeatureEnabled(await getFeatureFlags(), name)) notFound();
}
