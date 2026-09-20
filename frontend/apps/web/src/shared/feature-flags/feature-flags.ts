export type FeatureFlags = ReadonlyMap<string, boolean>;

export function isFeatureEnabled(flags: FeatureFlags, name: string): boolean {
  return flags.get(name) ?? true;
}
