import { spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { join } from "node:path";

import { repoRoot } from "./lib/walk.ts";

const webRoot = join(repoRoot, "frontend", "apps", "web");
const server = join(webRoot, ".next", "standalone", "apps", "web", "server.js");
const expectedMessage = "API_INTERNAL_URL is required when NODE_ENV is production";

if (!existsSync(server)) {
  console.error(`web startup guard: ${server} is missing; run "pnpm build" in frontend first`);
  process.exit(1);
}

const env = { ...process.env, NODE_ENV: "production", PORT: "3999", HOSTNAME: "127.0.0.1" };
delete env.API_INTERNAL_URL;

const result = spawnSync(process.execPath, [server], {
  cwd: webRoot,
  env,
  encoding: "utf8",
  timeout: 30_000,
  killSignal: "SIGKILL",
});
const output = `${result.stdout ?? ""}${result.stderr ?? ""}`;

if (result.signal !== null) {
  console.error(`web startup guard: the server kept running without API_INTERNAL_URL (killed after 30 s)\n${output}`);
  process.exit(1);
}
if (result.status !== 1 || !output.includes(expectedMessage)) {
  console.error(`web startup guard: expected exit code 1 naming API_INTERNAL_URL, got ${result.status}\n${output}`);
  process.exit(1);
}

console.log("web startup guard ok: the standalone server exits with code 1 and names API_INTERNAL_URL");
