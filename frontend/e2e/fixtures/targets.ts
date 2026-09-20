export const baseURL = process.env.E2E_BASE_URL ?? "http://localhost:3000";
export const apiBaseURL = process.env.E2E_API_BASE_URL ?? "http://localhost:5080";
export const startServers = !process.env.E2E_BASE_URL;
export const offlineBaseURL =
  process.env.E2E_OFFLINE_BASE_URL ?? (startServers ? "http://localhost:3001" : undefined);
export const unreachableApiURL = "http://127.0.0.1:1";
export const gatedApiBaseURL = process.env.E2E_GATED_API_BASE_URL ?? "http://localhost:5081";
export const gatedBaseURL =
  process.env.E2E_GATED_BASE_URL ?? (startServers ? "http://localhost:3002" : undefined);
export const gatedFeatureFlag = "Erp.Modules.Platform.SystemInfo";
