import { spawnSync } from "node:child_process";
import { randomUUID } from "node:crypto";
import { existsSync } from "node:fs";
import { request as httpRequest, type IncomingHttpHeaders, type IncomingMessage, type OutgoingHttpHeaders } from "node:http";
import { request } from "node:https";
import { join, posix, resolve } from "node:path";
import { setTimeout as delay } from "node:timers/promises";
import { connect, getCiphers, type ConnectionOptions } from "node:tls";
import { fileURLToPath } from "node:url";

import { imageInventoryProblems, isInventoryEvidence } from "../notices/lib/spdx.ts";
import { imageContentsProblems, imageNames, imageRepository, isRequiredFile, readFirstTarEntry, readSavedImage, repositoryCopies, type TarEntry } from "./lib/image-contents.ts";
import { repoRoot } from "./lib/walk.ts";

const composeDirectory = join(repoRoot, "infra", "compose");
const composeFiles = ["-f", "compose.yaml", "-f", "compose.override.yaml"];
const secretFiles = ["Erp__Platform__Database__ConnectionString", "Erp__Platform__Database__MigratorConnectionString", "Erp__Platform__Attachments__EncryptionKey", "Erp__Platform__Identity__ClientCertificate"];
const projectName = "erp-ai-pro-smoke";
const imageTag = process.env.IMAGE_TAG || "local";
const apiImage = `${imageRepository("api")}:${imageTag}`;
const proxyImage = `${imageRepository("proxy")}:${imageTag}`;
const apiBaseUrl = "http://127.0.0.1:5080";
const webBaseUrl = "http://127.0.0.1:3000";
const siteAddress = "127.0.0.1";
const proxyHttpPort = 8080;
const proxyHttpsPort = 8443;
const proxyRootCertificate = "/data/caddy/pki/authorities/local/root.crt";
const proxyUser = "caddy";
const proxyServingWait = 30_000;
const handshakeWait = 5_000;
const useShell = process.platform === "win32";
const keyRingDirectory = "/home/app/.local/share/ERP-AI-Pro/DataProtection-Keys";
const appUserId = 1654;
const proxyUserId = 10001;
const tarEntryKinds = new Map([
  ["5", "a directory"],
  ["0", "a file"],
  ["\0", "a file"],
]);
const edgeOwnedHeaders = ["server", "via", "alt-svc", "strict-transport-security"];
const nanosecondsPerSecond = 1_000_000_000;
const apiLongestTimeoutSeconds = 600;
const stallSeconds = 60;
const largestApiBodyBytes = 100 * 1024 * 1024 + 64 * 1024;
const serverFunctionBodyLimitBytes = 1024 * 1024;
const webServerBodyBufferBytes = 10 * 1024 * 1024;
const overWebLimitBytes = 3 * 1024 * 1024;

type Check = { name: string; url: string; status: number; redirect?: string; header?: [string, string]; body?: string };

type Expectation = Omit<Check, "url">;

type ProxyCheck = Expectation & { path: string; method?: "GET" | "POST"; form?: () => Buffer; host?: string; plain?: boolean };

type Answer = { status: number; location: string | null; header: (name: string) => string; body: () => Promise<string> };

type ProxyAnswer = { status: number; headers: IncomingHttpHeaders; body: string };

type Handshake = { protocol: string | null; cipher: string; alpn: string | false | null };

type LogEntry = { logger?: string; request?: { uri?: string; headers?: unknown }; resp_headers?: unknown };

type AdaptedHandler = { handler?: string; routes?: AdaptedRoute[]; max_size?: number; transport?: { response_header_timeout?: number; read_timeout?: number } };

type AdaptedRoute = { match?: { host?: string[]; path?: string[] }[]; handle?: AdaptedHandler[] };

type AdaptedServer = { read_timeout?: number; write_timeout?: number; read_idle_timeout?: number; write_idle_timeout?: number; routes?: AdaptedRoute[] };

export type AdaptedConfig = { apps?: { http?: { servers?: Record<string, AdaptedServer> } } };

export type ImageDirectory = { label: string; path: string; expected: TarEntry };

export const keyRing: ImageDirectory = { label: "api image", path: keyRingDirectory, expected: { name: `${posix.basename(keyRingDirectory)}/`, type: "5", mode: 0o700, uid: appUserId, gid: appUserId } };

export const proxyData: ImageDirectory = { label: "proxy image", path: "/data", expected: { name: "data/", type: "5", mode: 0o700, uid: proxyUserId, gid: proxyUserId } };

export const forwardSecretAeadSuite = /^ECDHE-(?:ECDSA|RSA)-(?:AES128-GCM-SHA256|AES256-GCM-SHA384|CHACHA20-POLY1305)$/;

const stackTenantIssuer = "https://login.microsoftonline.com/11111111-1111-1111-1111-111111111111/v2.0";

const unknownEntraSession = new URLSearchParams({ iss: stackTenantIssuer, sid: "00000000-0000-0000-0000-000000000000" });

const queryMarkers = { search: `smoke${randomUUID().replaceAll("-", "")}`, link: `smoke${randomUUID().replaceAll("-", "")}`, probe: `smoke${randomUUID().replaceAll("-", "")}` };

const unauthenticated = (name: string, url: string): Check => ({ name, url, status: 401, header: ["content-type", "application/problem+json"], body: '"request.unauthenticated"' });

const formBody = (fields: Record<string, string>): Buffer => Buffer.from(new URLSearchParams(fields).toString());

const checks: Check[] = [
  { name: "api liveness", url: `${apiBaseUrl}/healthz/live`, status: 200 },
  { name: "api readiness", url: `${apiBaseUrl}/healthz/ready`, status: 200 },
  unauthenticated("anonymous api system info", `${apiBaseUrl}/api/platform/system-info`),
  { name: "web health", url: `${webBaseUrl}/healthz`, status: 200 },
  { name: "web root redirects to login", url: `${webBaseUrl}/`, status: 307, redirect: "/login" },
  { name: "web login page", url: `${webBaseUrl}/login`, status: 200, header: ["content-security-policy", "'nonce-"] },
  { name: "web robots.txt without a session", url: `${webBaseUrl}/robots.txt`, status: 200, header: ["content-type", "text/plain"], body: "Disallow: /" },
  unauthenticated("anonymous api through the web origin", `${webBaseUrl}/api/platform/system-info`),
  unauthenticated("anonymous api feature flags", `${apiBaseUrl}/api/platform/features`),
  unauthenticated("anonymous feature flags through the web origin", `${webBaseUrl}/api/platform/features`),
  unauthenticated("anonymous api startups", `${apiBaseUrl}/api/platform/system-info/startups`),
  unauthenticated("anonymous startups through the web origin", `${webBaseUrl}/api/platform/system-info/startups`),
  unauthenticated("anonymous api attachments", `${apiBaseUrl}/api/platform/attachments`),
  unauthenticated("anonymous attachments through the web origin", `${webBaseUrl}/api/platform/attachments`),
  unauthenticated("anonymous unknown api route", `${apiBaseUrl}/api/platform/does-not-exist`),
  { name: "api front-channel sign-out of an unknown Entra session", url: `${apiBaseUrl}/api/auth/signout-oidc?${unknownEntraSession}`, status: 200 },
  { name: "front-channel sign-out through the web origin", url: `${webBaseUrl}/api/auth/signout-oidc?${unknownEntraSession}`, status: 200 },
  { name: "sign-in refuses a return address on another site through the web origin", url: `${webBaseUrl}/api/auth/login?returnUrl=https%3A%2F%2Fexample.com`, status: 400, header: ["content-type", "application/problem+json"], body: '"request.invalid"' },
];

// Each runs to the proxy's local address without a server name, as a browser that opens an IP address does, and over https
// trusts only the proxy's own root.
const proxyChecks: ProxyCheck[] = [
  { name: "login page through the proxy", path: "/login", status: 200, header: ["content-security-policy", "'nonce-"] },
  { name: "anonymous api through the proxy", path: "/api/platform/system-info", status: 401, header: ["x-content-type-options", "nosniff"], body: '"request.unauthenticated"' },
  { name: "sign-in refuses a return address on another site through the proxy", path: "/api/auth/login?returnUrl=https%3A%2F%2Fexample.com", status: 400, header: ["content-type", "application/problem+json"], body: '"request.invalid"' },
  { name: "a made-up answer from Microsoft through the proxy", path: "/api/auth/signin-oidc", method: "POST", form: () => formBody({ code: "made-up", state: "made-up" }), status: 302, redirect: "/login?error=sign-in-failed" },
  { name: "front-channel sign-out through the proxy", path: `/api/auth/signout-oidc?${unknownEntraSession}`, status: 200 },
  { name: "a body over the web limit reaches the api through the proxy", path: "/api/auth/signin-oidc", method: "POST", form: () => formBody({ code: "a".repeat(overWebLimitBytes), state: "made-up" }), status: 302, redirect: "/login?error=sign-in-failed" },
  { name: "the proxy refuses a body over the web limit", path: "/login", method: "POST", form: () => formBody({ field: "a".repeat(overWebLimitBytes) }), status: 413 },
  { name: "a directory search through the proxy", path: `/api/identity/users/directory?search=${queryMarkers.search}`, status: 401, body: '"request.unauthenticated"' },
  { name: "a download link through the proxy", path: `/api/platform/attachments/00000000-0000-0000-0000-000000000000/content?link=${queryMarkers.link}`, status: 401, body: '"request.unauthenticated"' },
  { name: "proxy redirects http to https", path: "/login", plain: true, status: 308, redirect: `https://${siteAddress}/login` },
  { name: "proxy redirects http naming another host to its own address", path: "/", plain: true, host: "example.com", status: 308, redirect: `https://${siteAddress}/` },
  { name: "proxy refuses https naming another host", path: "/", host: "example.com", status: 421 },
];

const stoppedApiCheck: ProxyCheck = { name: "the proxy answers for a stopped api", path: `/api/platform/system-info?probe=${queryMarkers.probe}`, status: 502 };

function describeTarEntry(entry: TarEntry): string {
  const kind = tarEntryKinds.get(entry.type) ?? `a tar entry of type '${entry.type}'`;
  return `${kind} owned by ${entry.uid}:${entry.gid} with mode ${(entry.mode & 0o7777).toString(8).padStart(4, "0")}`;
}

export function directoryProblem(directory: ImageDirectory, entry: TarEntry): string | undefined {
  if (entry.name !== directory.expected.name) return `${directory.label} ${directory.path}: expected the archive to start with "${directory.expected.name}", got "${entry.name}"`;
  const expected = describeTarEntry(directory.expected);
  const actual = describeTarEntry(entry);
  return actual === expected ? undefined : `${directory.label} ${directory.path}: expected ${expected}, got ${actual}`;
}

export function tls12SuiteProblem(accepted: readonly string[]): string | undefined {
  if (accepted.length === 0) return "proxy TLS: no TLS 1.2 suite was accepted";
  const weak = accepted.filter((suite) => !forwardSecretAeadSuite.test(suite));
  return weak.length === 0 ? undefined : `proxy TLS: accepted TLS 1.2 suites that are not forward-secret AEAD: ${weak.join(", ")}`;
}

export function proxyLogProblems(log: string, markers: readonly string[]): string[] {
  const lines = log.split(/\r?\n/).filter((line) => line.includes("{"));
  const problems = markers.filter((marker) => lines.some((line) => line.includes(marker))).map((marker) => `proxy log: holds the query value ${marker}`);
  let accessEntries = 0;
  for (const line of lines) {
    let entry: LogEntry;
    try {
      entry = JSON.parse(line.slice(line.indexOf("{"))) as LogEntry;
    } catch {
      problems.push(`proxy log: a line is not JSON: ${line.slice(0, 120)}`);
      continue;
    }
    const logger = entry.logger ?? "";
    if (!logger.startsWith("http.log.")) continue;
    if (logger.startsWith("http.log.access")) accessEntries++;
    const uri = entry.request?.uri ?? "";
    if (uri.includes("?")) problems.push(`proxy log: ${logger} holds the query string of ${uri.slice(0, uri.indexOf("?"))}`);
    if (entry.request?.headers !== undefined) problems.push(`proxy log: ${logger} holds request headers`);
    if (entry.resp_headers !== undefined) problems.push(`proxy log: ${logger} holds response headers`);
  }
  if (accessEntries === 0) problems.push("proxy log: holds no access entry");
  return problems;
}

function walkHandlers(routes: readonly AdaptedRoute[] | undefined, paths: readonly string[], visit: (handler: AdaptedHandler, paths: readonly string[]) => void): void {
  for (const route of routes ?? []) {
    const matched = route.match?.flatMap((match) => match.path ?? []) ?? [];
    const scope = matched.length > 0 ? matched : paths;
    for (const handler of route.handle ?? []) {
      visit(handler, scope);
      walkHandlers(handler.routes, scope, visit);
    }
  }
}

function seconds(nanoseconds: number | undefined): string {
  return `${(nanoseconds ?? 0) / nanosecondsPerSecond} s`;
}

function serverProblems(name: string, server: AdaptedServer): string[] {
  const ceilings = (["read_timeout", "write_timeout"] as const)
    .filter((key) => (server[key] ?? 0) <= apiLongestTimeoutSeconds * nanosecondsPerSecond)
    .map((key) => `proxy configuration: ${name} ${key} is ${seconds(server[key])}, not above ${apiLongestTimeoutSeconds} s`);
  const stalls = (["read_idle_timeout", "write_idle_timeout"] as const)
    .filter((key) => server[key] !== stallSeconds * nanosecondsPerSecond)
    .map((key) => `proxy configuration: ${name} ${key} is ${seconds(server[key])}, not ${stallSeconds} s`);
  return [...ceilings, ...stalls];
}

function upstreamProblems(name: string, handler: AdaptedHandler): string[] {
  return (["response_header_timeout", "read_timeout"] as const)
    .filter((key) => (handler.transport?.[key] ?? 0) <= apiLongestTimeoutSeconds * nanosecondsPerSecond)
    .map((key) => `proxy configuration: ${name} upstream ${key} is ${seconds(handler.transport?.[key])}, not above ${apiLongestTimeoutSeconds} s`);
}

function bodyLimitProblems(apiLimits: readonly number[], otherLimits: readonly number[]): string[] {
  return [
    ...(apiLimits.length === 0 ? ["proxy configuration: no request body limit on /api/*"] : []),
    ...apiLimits.filter((limit) => limit <= largestApiBodyBytes).map((limit) => `proxy configuration: the request body limit on /api/* is ${limit} bytes, not above ${largestApiBodyBytes}`),
    ...(otherLimits.length === 0 ? ["proxy configuration: no request body limit outside /api/*"] : []),
    ...otherLimits
      .filter((limit) => limit <= serverFunctionBodyLimitBytes || limit >= webServerBodyBufferBytes)
      .map((limit) => `proxy configuration: the request body limit outside /api/* is ${limit} bytes, not between ${serverFunctionBodyLimitBytes} and ${webServerBodyBufferBytes}`),
  ];
}

export function adaptedConfigProblems(config: AdaptedConfig): string[] {
  const servers = Object.entries(config.apps?.http?.servers ?? {});
  if (servers.length === 0) return ["proxy configuration: holds no server"];
  const problems: string[] = [];
  const apiLimits: number[] = [];
  const otherLimits: number[] = [];
  for (const [name, server] of servers) {
    problems.push(...serverProblems(name, server));
    walkHandlers(server.routes, [], (handler, paths) => {
      if (handler.handler === "reverse_proxy") problems.push(...upstreamProblems(name, handler));
      if (handler.handler === "request_body") (paths.includes("/api/*") ? apiLimits : otherLimits).push(handler.max_size ?? 0);
    });
  }
  return [...problems, ...bodyLimitProblems(apiLimits, otherLimits)];
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

function composeOutput(...args: string[]): { status: number | null; stdout: string; stderr: string } {
  return spawnSync("docker", ["compose", "-p", projectName, ...composeFiles, ...args], {
    cwd: composeDirectory,
    encoding: "utf8",
    shell: useShell,
    env: { ...process.env, IMAGE_TAG: imageTag },
    maxBuffer: 64 * 1024 * 1024,
  });
}

function migratorOutcome(): string | undefined {
  const result = composeOutput("ps", "--all", "--format", "{{.State}}:{{.ExitCode}}", "migrator");
  const outcome = result.stdout.trim();
  return outcome === "exited:0" ? undefined : `migrator: expected "exited:0", got "${outcome || result.stderr.trim()}"`;
}

function directoryOutcome(image: string, directory: ImageDirectory): string | undefined {
  const created = spawnSync("docker", ["create", "--pull", "never", image], { encoding: "utf8", shell: useShell });
  if (created.status !== 0) return `${directory.label}: docker create ${image} exited with ${created.status}: ${created.stderr.trim()}`;
  const container = created.stdout.trim();
  try {
    const archive = spawnSync("docker", ["cp", `${container}:${directory.path}`, "-"], { shell: useShell });
    if (archive.status !== 0) return `${directory.label} ${directory.path}: docker cp exited with ${archive.status}: ${archive.stderr.toString().trim()}`;
    return directoryProblem(directory, readFirstTarEntry(archive.stdout));
  } catch (error) {
    return `${directory.label} ${directory.path}: ${(error as Error).message}`;
  } finally {
    spawnSync("docker", ["rm", container], { stdio: ["ignore", "ignore", "inherit"], shell: useShell });
  }
}

async function imageContentsOutcomes(): Promise<string[]> {
  const outcomes: string[] = [];
  for (const image of imageNames) {
    const reference = `${imageRepository(image)}:${imageTag}`;
    try {
      const archive = await readSavedImage(reference, (path, size) => isRequiredFile(path, size) || isInventoryEvidence(path));
      outcomes.push(
        ...imageContentsProblems(image, archive, repositoryCopies(repoRoot, image)).map((problem) => `${image} image contents: ${problem}`),
        ...imageInventoryProblems(image, archive).map((problem) => `${image} image inventory: ${problem}`),
      );
    } catch (error) {
      outcomes.push(`${image} image contents: ${(error as Error).message}`);
    }
  }
  return outcomes;
}

function imageUserOutcome(image: string, expected: string): string | undefined {
  const inspected = spawnSync("docker", ["image", "inspect", "--format", "{{.Config.User}}", image], { encoding: "utf8", shell: useShell });
  if (inspected.status !== 0) return `proxy image: docker image inspect ${image} exited with ${inspected.status}: ${inspected.stderr.trim()}`;
  const user = inspected.stdout.trim() || "root";
  return user === expected ? undefined : `proxy image: expected the user ${expected}, got ${user}`;
}

async function problemOf(check: Expectation, answer: Answer): Promise<string | undefined> {
  if (answer.status !== check.status) return `${check.name}: expected ${check.status}, got ${answer.status}`;
  if (check.redirect && answer.location !== check.redirect) {
    return `${check.name}: expected a redirect to ${check.redirect}, got ${answer.location}`;
  }
  if (check.header) {
    const [name, expected] = check.header;
    const actual = answer.header(name);
    if (!actual.includes(expected)) return `${check.name}: header ${name} "${actual}" does not contain "${expected}"`;
  }
  if (check.body && !(await answer.body()).includes(check.body)) return `${check.name}: body does not contain "${check.body}"`;
  return undefined;
}

async function verify(check: Check): Promise<string | undefined> {
  const response = await fetch(check.url, { redirect: "manual" });
  return problemOf(check, { status: response.status, location: response.headers.get("location"), header: (name) => response.headers.get(name) ?? "", body: () => response.text() });
}

function sendThroughProxy(check: Pick<ProxyCheck, "path" | "method" | "form" | "host" | "plain">, root: string): Promise<ProxyAnswer> {
  return new Promise((resolveAnswer, reject) => {
    const headers: OutgoingHttpHeaders = {};
    const form = check.form?.();
    if (check.host) headers.host = check.host;
    if (form) {
      headers["content-type"] = "application/x-www-form-urlencoded";
      headers["content-length"] = form.length;
    }
    const collect = (incoming: IncomingMessage) => {
      const chunks: Buffer[] = [];
      incoming.on("data", (chunk: Buffer) => chunks.push(chunk));
      incoming.on("error", reject);
      incoming.on("end", () => resolveAnswer({ status: incoming.statusCode ?? 0, headers: incoming.headers, body: Buffer.concat(chunks).toString("utf8") }));
    };
    const options = { host: siteAddress, path: check.path, method: check.method ?? "GET", headers, agent: false };
    const outgoing = check.plain ? httpRequest({ ...options, port: proxyHttpPort }, collect) : request({ ...options, port: proxyHttpsPort, ca: root, servername: "" }, collect);
    outgoing.on("error", reject);
    outgoing.end(form);
  });
}

async function verifyThroughProxy(check: ProxyCheck, root: string): Promise<string | undefined> {
  try {
    const answer = await sendThroughProxy(check, root);
    const header = (name: string) => {
      const value = answer.headers[name];
      return Array.isArray(value) ? value.join(", ") : (value ?? "");
    };
    const edgeHeader = edgeOwnedHeaders.find((name) => header(name) !== "");
    if (edgeHeader) return `${check.name}: carries the header ${edgeHeader} "${header(edgeHeader)}"`;
    return await problemOf(check, { status: answer.status, location: answer.headers.location ?? null, header, body: () => Promise.resolve(answer.body) });
  } catch (error) {
    return `${check.name}: ${(error as Error).message}`;
  }
}

function handshake(root: string, options: ConnectionOptions): Promise<Handshake | Error> {
  return new Promise((resolveHandshake) => {
    const socket = connect({ host: siteAddress, port: proxyHttpsPort, ca: root, ...options }, () => {
      resolveHandshake({ protocol: socket.getProtocol(), cipher: socket.getCipher().name, alpn: socket.alpnProtocol });
      socket.end();
    });
    socket.setTimeout(handshakeWait, () => socket.destroy(new Error(`no TLS answer within ${handshakeWait / 1000} seconds`)));
    socket.on("error", resolveHandshake);
  });
}

async function refusedVersionOutcome(root: string, version: "TLSv1" | "TLSv1.1"): Promise<string | undefined> {
  const answer = await handshake(root, { minVersion: version, maxVersion: version, ciphers: "DEFAULT@SECLEVEL=0" });
  if (answer instanceof Error && (answer as NodeJS.ErrnoException).code === "ERR_SSL_TLSV1_ALERT_PROTOCOL_VERSION") return undefined;
  const outcome = answer instanceof Error ? answer.message : answer.protocol;
  return `proxy TLS: expected ${version} to be refused, got ${outcome}`;
}

async function acceptedTls12Suites(root: string): Promise<string[]> {
  const accepted: string[] = [];
  for (const suite of getCiphers().map((name) => name.toUpperCase()).filter((name) => !name.startsWith("TLS_"))) {
    const answer = await handshake(root, { minVersion: "TLSv1.2", maxVersion: "TLSv1.2", ciphers: `${suite}:@SECLEVEL=0` });
    if (!(answer instanceof Error)) accepted.push(answer.cipher);
  }
  return accepted;
}

async function alpnOutcome(root: string, offered: readonly string[], expected: string): Promise<string | undefined> {
  const answer = await handshake(root, { ALPNProtocols: [...offered] });
  const negotiated = answer instanceof Error ? answer.message : answer.alpn;
  return negotiated === expected ? undefined : `proxy TLS: offered ${offered.join(" and ")}, expected ${expected}, got ${negotiated}`;
}

async function tlsOutcomes(root: string): Promise<(string | undefined)[]> {
  const modern = await handshake(root, { minVersion: "TLSv1.3", maxVersion: "TLSv1.3" });
  return [
    await refusedVersionOutcome(root, "TLSv1"),
    await refusedVersionOutcome(root, "TLSv1.1"),
    modern instanceof Error ? `proxy TLS: TLS 1.3 was refused: ${modern.message}` : undefined,
    tls12SuiteProblem(await acceptedTls12Suites(root)),
    await alpnOutcome(root, ["h2", "http/1.1"], "h2"),
    await alpnOutcome(root, ["http/1.1"], "http/1.1"),
  ];
}

function proxyRoot(): string | undefined {
  const result = composeOutput("exec", "-T", "proxy", "cat", proxyRootCertificate);
  return result.status === 0 && result.stdout.includes("-----BEGIN CERTIFICATE-----") ? result.stdout : undefined;
}

// The proxy reports healthy once it runs, which can be before it has issued the certificate of its address.
async function rootOfServingProxy(): Promise<string | Error> {
  const deadline = Date.now() + proxyServingWait;
  let last = `${proxyRootCertificate} is not readable`;
  for (;;) {
    const root = proxyRoot();
    if (root !== undefined) {
      try {
        await sendThroughProxy({ path: "/healthz" }, root);
        return root;
      } catch (error) {
        last = (error as Error).message;
      }
    }
    if (Date.now() >= deadline) return new Error(`proxy: no answer over https within ${proxyServingWait / 1000} seconds (${last})`);
    await delay(1_000);
  }
}

function adaptedConfigOutcomes(): string[] {
  const adapted = composeOutput("exec", "-T", "proxy", "caddy", "adapt", "--config", "/etc/caddy/Caddyfile");
  if (adapted.status !== 0) return [`proxy configuration: caddy adapt exited with ${adapted.status}: ${adapted.stderr.trim()}`];
  try {
    return adaptedConfigProblems(JSON.parse(adapted.stdout) as AdaptedConfig);
  } catch (error) {
    return [`proxy configuration: ${(error as Error).message}`];
  }
}

async function proxyOutcomes(): Promise<(string | undefined)[]> {
  const root = await rootOfServingProxy();
  if (root instanceof Error) return [root.message];
  const outcomes = [...(await tlsOutcomes(root)), ...(await Promise.all(proxyChecks.map((check) => verifyThroughProxy(check, root)))), ...adaptedConfigOutcomes()];
  compose("stop", "api");
  outcomes.push(await verifyThroughProxy(stoppedApiCheck, root));
  const log = composeOutput("logs", "--no-color", "proxy");
  return [...outcomes, ...proxyLogProblems(`${log.stdout}\n${log.stderr}`, Object.values(queryMarkers))];
}

async function main(): Promise<void> {
  const missingSecrets = secretFiles.filter((name) => !existsSync(join(composeDirectory, "secrets", name)));
  if (missingSecrets.length > 0) {
    console.error(`compose smoke: ${new Intl.ListFormat("en", { type: "conjunction" }).format(missingSecrets.map((name) => `infra/compose/secrets/${name}`))} missing; create them as described in docs/guides/local-development.md`);
    process.exit(1);
  }

  let failures: string[] = await imageContentsOutcomes();
  try {
    compose("up", "--detach", "--wait", "--wait-timeout", "180", "--no-build");
    const outcomes = [directoryOutcome(apiImage, keyRing), directoryOutcome(proxyImage, proxyData), imageUserOutcome(proxyImage, proxyUser), migratorOutcome(), ...(await Promise.all(checks.map(verify))), ...(await proxyOutcomes())];
    failures = [...failures, ...outcomes.filter((f): f is string => f !== undefined)];
  } finally {
    if (failures.length > 0) compose("logs", "--no-color", "--tail", "100");
    compose("down", "--remove-orphans", "--volumes", "--timeout", "10");
  }

  if (failures.length > 0) {
    console.error("compose smoke failed:");
    for (const failure of failures) console.error(`  ${failure}`);
    process.exit(1);
  }

  console.log(`compose smoke ok: the ${imageNames.join(", ")} images hold no forbidden file, the licence and notices files of this checkout and only their own labels, and notices that list exactly the packages they hold, the api image holds ${keyRingDirectory} as ${describeTarEntry(keyRing.expected)}, the proxy image runs as ${proxyUser} and holds /data as ${describeTarEntry(proxyData.expected)}, the migrator completed, ${checks.length} checks passed against the api and the web container, and through the proxy its TLS policy, its configured limits and timeouts, ${proxyChecks.length + 1} requests and a log without query strings or headers`);
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  await main();
}
