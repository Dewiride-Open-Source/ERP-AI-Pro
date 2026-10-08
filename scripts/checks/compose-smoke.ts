import { spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { join, posix, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { repoRoot } from "./lib/walk.ts";

const composeDirectory = join(repoRoot, "infra", "compose");
const composeFiles = ["-f", "compose.yaml", "-f", "compose.override.yaml"];
const secretFiles = ["Erp__Platform__Database__ConnectionString", "Erp__Platform__Database__MigratorConnectionString", "Erp__Platform__Attachments__EncryptionKey", "Erp__Platform__Identity__ClientCertificate"];
const projectName = "erp-ai-pro-smoke";
const imageTag = process.env.IMAGE_TAG || "local";
const apiImage = `ghcr.io/dewiride-open-source/erp-ai-pro/api:${imageTag}`;
const apiBaseUrl = "http://127.0.0.1:5080";
const webBaseUrl = "http://127.0.0.1:3000";
const useShell = process.platform === "win32";
const keyRingDirectory = "/home/app/.local/share/ERP-AI-Pro/DataProtection-Keys";
const appUserId = 1654;
const tarHeaderSize = 512;
const tarEntryKinds = new Map([
  ["5", "a directory"],
  ["0", "a file"],
  ["\0", "a file"],
]);

type Check = { name: string; url: string; status: number; redirect?: string; header?: [string, string]; body?: string };

export type TarEntry = { name: string; type: string; mode: number; uid: number; gid: number };

const expectedKeyRingEntry: TarEntry = { name: `${posix.basename(keyRingDirectory)}/`, type: "5", mode: 0o700, uid: appUserId, gid: appUserId };

const unknownEntraSession = new URLSearchParams({ iss: "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/v2.0", sid: "00000000-0000-0000-0000-000000000000" });

const unauthenticated = (name: string, url: string): Check => ({ name, url, status: 401, header: ["content-type", "application/problem+json"], body: '"request.unauthenticated"' });

const checks: Check[] = [
  { name: "api liveness", url: `${apiBaseUrl}/healthz/live`, status: 200 },
  { name: "api readiness", url: `${apiBaseUrl}/healthz/ready`, status: 200 },
  unauthenticated("anonymous api system info", `${apiBaseUrl}/api/platform/system-info`),
  { name: "web health", url: `${webBaseUrl}/healthz`, status: 200 },
  { name: "web root redirects to login", url: `${webBaseUrl}/`, status: 307, redirect: "/login" },
  { name: "web login page", url: `${webBaseUrl}/login`, status: 200, header: ["content-security-policy", "'nonce-"] },
  unauthenticated("anonymous api through the web origin", `${webBaseUrl}/api/platform/system-info`),
  unauthenticated("anonymous api feature flags", `${apiBaseUrl}/api/platform/features`),
  unauthenticated("anonymous feature flags through the web origin", `${webBaseUrl}/api/platform/features`),
  unauthenticated("anonymous api startups", `${apiBaseUrl}/api/platform/system-info/startups`),
  unauthenticated("anonymous startups through the web origin", `${webBaseUrl}/api/platform/system-info/startups`),
  unauthenticated("anonymous unknown api route", `${apiBaseUrl}/api/platform/does-not-exist`),
  { name: "api front-channel sign-out of an unknown Entra session", url: `${apiBaseUrl}/api/auth/signout-oidc?${unknownEntraSession}`, status: 200 },
  { name: "front-channel sign-out through the web origin", url: `${webBaseUrl}/api/auth/signout-oidc?${unknownEntraSession}`, status: 200 },
  { name: "sign-in refuses a return address on another site through the web origin", url: `${webBaseUrl}/api/auth/login?returnUrl=https%3A%2F%2Fexample.com`, status: 400, header: ["content-type", "application/problem+json"], body: '"request.invalid"' },
];

function tarText(header: Uint8Array, offset: number, length: number): string {
  const field = header.subarray(offset, offset + length);
  const end = field.indexOf(0);
  return new TextDecoder().decode(end === -1 ? field : field.subarray(0, end));
}

function tarOctal(header: Uint8Array, offset: number, length: number): number {
  const digits = tarText(header, offset, length).trim();
  if (!/^[0-7]+$/.test(digits)) throw new Error(`the tar header field at byte ${offset} is not an octal number ("${digits}")`);
  return Number.parseInt(digits, 8);
}

export function readFirstTarEntry(archive: Uint8Array): TarEntry {
  if (archive.length < tarHeaderSize) throw new Error(`the archive holds ${archive.length} bytes, less than one tar header`);
  const header = archive.subarray(0, tarHeaderSize);
  if (!tarText(header, 257, 6).startsWith("ustar")) throw new Error("the archive does not start with a ustar header");
  const prefix = tarText(header, 345, 155);
  const name = tarText(header, 0, 100);
  return {
    name: prefix === "" ? name : `${prefix}/${name}`,
    type: String.fromCodePoint(header[156] ?? 0),
    mode: tarOctal(header, 100, 8),
    uid: tarOctal(header, 108, 8),
    gid: tarOctal(header, 116, 8),
  };
}

function describeTarEntry(entry: TarEntry): string {
  const kind = tarEntryKinds.get(entry.type) ?? `a tar entry of type '${entry.type}'`;
  return `${kind} owned by ${entry.uid}:${entry.gid} with mode ${(entry.mode & 0o7777).toString(8).padStart(4, "0")}`;
}

export function keyRingDirectoryProblem(entry: TarEntry): string | undefined {
  if (entry.name !== expectedKeyRingEntry.name) return `api image ${keyRingDirectory}: expected the archive to start with "${expectedKeyRingEntry.name}", got "${entry.name}"`;
  const expected = describeTarEntry(expectedKeyRingEntry);
  const actual = describeTarEntry(entry);
  return actual === expected ? undefined : `api image ${keyRingDirectory}: expected ${expected}, got ${actual}`;
}

function compose(...args: string[]): void {
  const result = spawnSync("docker", ["compose", "-p", projectName, ...composeFiles, ...args], {
    cwd: composeDirectory,
    stdio: "inherit",
    shell: useShell,
    env: { ...process.env, IMAGE_TAG: imageTag },
  });
  if (result.status !== 0) throw new Error(`docker compose ${args.join(" ")} exited with ${result.status}`);
}

function migratorOutcome(): string | undefined {
  const result = spawnSync("docker", ["compose", "-p", projectName, ...composeFiles, "ps", "--all", "--format", "{{.State}}:{{.ExitCode}}", "migrator"], {
    cwd: composeDirectory,
    encoding: "utf8",
    shell: useShell,
    env: { ...process.env, IMAGE_TAG: imageTag },
  });
  const outcome = result.stdout.trim();
  return outcome === "exited:0" ? undefined : `migrator: expected "exited:0", got "${outcome || result.stderr.trim()}"`;
}

export function keyRingDirectoryOutcome(image: string): string | undefined {
  const created = spawnSync("docker", ["create", "--pull", "never", image], { encoding: "utf8", shell: useShell });
  if (created.status !== 0) return `api image: docker create ${image} exited with ${created.status}: ${created.stderr.trim()}`;
  const container = created.stdout.trim();
  try {
    const archive = spawnSync("docker", ["cp", `${container}:${keyRingDirectory}`, "-"], { shell: useShell });
    if (archive.status !== 0) return `api image ${keyRingDirectory}: docker cp exited with ${archive.status}: ${archive.stderr.toString().trim()}`;
    return keyRingDirectoryProblem(readFirstTarEntry(archive.stdout));
  } catch (error) {
    return `api image ${keyRingDirectory}: ${(error as Error).message}`;
  } finally {
    spawnSync("docker", ["rm", container], { stdio: ["ignore", "ignore", "inherit"], shell: useShell });
  }
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

async function main(): Promise<void> {
  const missingSecrets = secretFiles.filter((name) => !existsSync(join(composeDirectory, "secrets", name)));
  if (missingSecrets.length > 0) {
    console.error(`compose smoke: ${new Intl.ListFormat("en", { type: "conjunction" }).format(missingSecrets.map((name) => `infra/compose/secrets/${name}`))} missing; create them as described in docs/guides/local-development.md`);
    process.exit(1);
  }

  let failures: string[] = [];
  try {
    compose("up", "--detach", "--wait", "--wait-timeout", "180", "--no-build");
    failures = [keyRingDirectoryOutcome(apiImage), migratorOutcome(), ...(await Promise.all(checks.map(verify)))].filter((f): f is string => f !== undefined);
  } finally {
    if (failures.length > 0) compose("logs", "--no-color", "--tail", "100");
    compose("down", "--remove-orphans", "--volumes", "--timeout", "10");
  }

  if (failures.length > 0) {
    console.error("compose smoke failed:");
    for (const failure of failures) console.error(`  ${failure}`);
    process.exit(1);
  }

  console.log(`compose smoke ok: the api image holds ${keyRingDirectory} as ${describeTarEntry(expectedKeyRingEntry)}, the migrator completed and ${checks.length} checks passed against the running stack`);
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  await main();
}
