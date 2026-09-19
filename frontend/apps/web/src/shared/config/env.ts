import "server-only";

export const serverEnv = {
  apiInternalUrl: (process.env.API_INTERNAL_URL ?? "http://localhost:5080").replace(/\/+$/, ""),
} as const;
