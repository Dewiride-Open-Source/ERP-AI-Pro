export const baseURL = process.env.E2E_BASE_URL ?? "https://localhost:3200";
export const apiBaseURL = process.env.E2E_API_BASE_URL ?? "http://127.0.0.1:5180";
export const startServers = !process.env.E2E_BASE_URL;
export const offlineBaseURL =
  process.env.E2E_OFFLINE_BASE_URL ?? (startServers ? "https://localhost:3201" : undefined);
export const unreachableApiURL = "http://127.0.0.1:1";
export const gatedApiBaseURL = process.env.E2E_GATED_API_BASE_URL ?? "http://127.0.0.1:5181";
export const gatedBaseURL =
  process.env.E2E_GATED_BASE_URL ?? (startServers ? "https://localhost:3202" : undefined);
export const gatedFeatureFlags = [
  "Erp.Modules.Platform.SystemInfo",
  "Erp.Modules.Platform.Attachments",
] as const;
