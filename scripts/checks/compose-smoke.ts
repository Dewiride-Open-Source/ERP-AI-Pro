import { spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { join } from "node:path";

import { repoRoot } from "./lib/walk.ts";

const composeDirectory = join(repoRoot, "infra", "compose");
const composeFiles = ["-f", "compose.yaml", "-f", "compose.override.yaml"];
const databaseSecretFile = join(composeDirectory, "secrets", "Erp__Platform__Database__ConnectionString");
const projectName = "erp-ai-pro-smoke";
const apiBaseUrl = "http://127.0.0.1:5080";
const webBaseUrl = "http://127.0.0.1:3000";

type Check = { name: string; url: string; status: number; redirect?: string; header?: [string, string]; body?: string };

const checks: Check[] = [
  { name: "api liveness", url: `${apiBaseUrl}/healthz/live`, status: 200 },
  { name: "api readiness", url: `${apiBaseUrl}/healthz/ready`, status: 200 },
  { name: "api system info", url: `${apiBaseUrl}/api/platform/system-info`, status: 200, header: ["content-type", "application/json"] },
  { name: "web health", url: `${webBaseUrl}/healthz`, status: 200 },
  { name: "web root redirects to login", url: `${webBaseUrl}/`, status: 307, redirect: "/login" },
  { name: "web login page", url: `${webBaseUrl}/login`, status: 200, header: ["content-security-policy", "'nonce-"] },
  { name: "api through the web origin", url: `${webBaseUrl}/api/platform/system-info`, status: 200, header: ["content-type", "application/json"] },
  { name: "api feature flags", url: `${apiBaseUrl}/api/platform/features`, status: 200, header: ["content-type", "application/json"], body: "Erp.Modules.Platform.SystemInfo" },
  { name: "feature flags through the web origin", url: `${webBaseUrl}/api/platform/features`, status: 200, header: ["content-type", "application/json"], body: "Erp.Modules.Platform.SystemInfo" },
  { name: "api startups", url: `${apiBaseUrl}/api/platform/system-info/startups`, status: 200, header: ["content-type", "application/json"], body: '"startups"' },
  { name: "startups through the web origin", url: `${webBaseUrl}/api/platform/system-info/startups`, status: 200, header: ["content-type", "application/json"], body: '"startups"' },
];

function compose(...args: string[]): void {
  const result = spawnSync("docker", ["compose", "-p", projectName, ...composeFiles, ...args], {
    cwd: composeDirectory,
    stdio: "inherit",
    shell: process.platform === "win32",
    env: { ...process.env, IMAGE_TAG: process.env.IMAGE_TAG ?? "local" },
  });
  if (result.status !== 0) throw new Error(`docker compose ${args.join(" ")} exited with ${result.status}`);
}

async function verify(check: Check): Promise<string | undefined> {
  const response = await fetch(check.url, { redirect: "manual" });
  if (response.status !== check.status) return `${check.name}: expected ${check.status}, got ${response.status}`;
  if (check.redirect && !(response.headers.get("location") ?? "").endsWith(check.redirect)) {
    return `${check.name}: expected a redirect to ${check.redirect}, got ${response.headers.get("location")}`;
  }
  if (check.header) {
    const [name, expected] = check.header;
    const actual = response.headers.get(name) ?? "";
    if (!actual.includes(expected)) return `${check.name}: header ${name} "${actual}" does not contain "${expected}"`;
  }
  if (check.body && !(await response.text()).includes(check.body)) return `${check.name}: body does not contain "${check.body}"`;
  return undefined;
}

if (!existsSync(databaseSecretFile)) {
  console.error("compose smoke: infra/compose/secrets/Erp__Platform__Database__ConnectionString is missing; create it as described in docs/guides/local-development.md");
  process.exit(1);
}

let failures: string[] = [];
try {
  compose("up", "--detach", "--wait", "--wait-timeout", "180", "--no-build");
  failures = (await Promise.all(checks.map(verify))).filter((f): f is string => f !== undefined);
} finally {
  if (failures.length > 0) compose("logs", "--no-color", "--tail", "100");
  compose("down", "--remove-orphans", "--timeout", "10");
}

if (failures.length > 0) {
  console.error("compose smoke failed:");
  for (const failure of failures) console.error(`  ${failure}`);
  process.exit(1);
}

console.log(`compose smoke ok: ${checks.length} checks passed against the running stack`);
