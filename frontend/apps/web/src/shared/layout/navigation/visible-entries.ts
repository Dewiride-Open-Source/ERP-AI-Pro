import { mayShow } from "../../auth/permissions.ts";
import type { HeldSession } from "../../auth/session.ts";
import { isFeatureEnabled, type FeatureFlags } from "../../feature-flags/feature-flags.ts";
import type { NavigationEntry } from "../navigation-entry.ts";

export function visibleEntries<TEntry extends Pick<NavigationEntry, "featureFlag" | "permission">>(
  navigation: readonly TEntry[],
  flags: FeatureFlags,
  session: HeldSession,
): readonly TEntry[] {
  return navigation.filter(
    (entry) => isFeatureEnabled(flags, entry.featureFlag) && mayShow(session, entry.permission),
  );
}
