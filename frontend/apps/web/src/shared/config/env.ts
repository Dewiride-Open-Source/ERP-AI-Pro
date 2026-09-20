import "server-only";

import { readServerEnv, type ServerEnv } from "./env.schema";

let cached: ServerEnv | undefined;

export function serverEnv(): ServerEnv {
  cached ??= readServerEnv(process.env);
  return cached;
}
