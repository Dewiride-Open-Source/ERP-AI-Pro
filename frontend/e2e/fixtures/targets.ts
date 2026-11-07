import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

type FeatureFlagSeed = { readonly flags: readonly { readonly id: string }[] };

const featureFlagSeed = join(
  dirname(fileURLToPath(import.meta.url)),
  "../../../infra/appconfig/feature-flags.json",
);
const moduleFlag = /^Erp\.Modules\.[A-Za-z0-9]+\.[A-Za-z0-9]+$/;

export const baseURL = process.env.E2E_BASE_URL ?? "https://localhost:3200";
export const apiBaseURL = process.env.E2E_API_BASE_URL ?? "http://127.0.0.1:5180";
export const productionURL = process.env.E2E_PRODUCTION_URL;
export const startServers = !process.env.E2E_BASE_URL && !productionURL;
export const offlineBaseURL =
  process.env.E2E_OFFLINE_BASE_URL ?? (startServers ? "https://localhost:3201" : undefined);
export const unreachableApiURL = "http://127.0.0.1:1";
export const gatedApiBaseURL = process.env.E2E_GATED_API_BASE_URL ?? "http://127.0.0.1:5181";
export const gatedBaseURL =
  process.env.E2E_GATED_BASE_URL ?? (startServers ? "https://localhost:3202" : undefined);
export const gatedFeatureFlags: readonly string[] = (
  JSON.parse(readFileSync(featureFlagSeed, "utf8")) as FeatureFlagSeed
).flags
  .map((flag) => flag.id)
  .filter((id) => moduleFlag.test(id));
