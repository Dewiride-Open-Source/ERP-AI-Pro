import { spawnSync } from "node:child_process";
import { existsSync, rmSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { parseArgs } from "node:util";
import { fileURLToPath } from "node:url";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
const backend = join(repoRoot, "backend");
const frontend = join(repoRoot, "frontend");
const isWindows = process.platform === "win32";
const backendTestVariables = ["ERP_TEST_SQL_CONNECTION", "ERP_TEST_BLOB_EMULATOR_HOST"];
const bash = isWindows ? gitBashPath() : "bash";

const { values } = parseArgs({
  options: {
    e2e: { type: "boolean", default: false },
    docker: { type: "boolean", default: false },
    "skip-backend": { type: "boolean", default: false },
    "skip-frontend": { type: "boolean", default: false },
    help: { type: "boolean", default: false },
  },
});

if (values.help) {
  console.log("Usage: node scripts/verify/verify.ts [--e2e] [--docker] [--skip-backend] [--skip-frontend]");
  process.exit(0);
}

type Step = { name: string; cwd: string; command: string; args: string[]; env?: Record<string, string>; shell?: boolean };

const steps: Step[] = [];

if (!values["skip-backend"]) {
  const missing = backendTestVariables.filter((name) => !process.env[name]);
  if (missing.length > 0) {
    console.error(
      `✖ ${new Intl.ListFormat("en", { type: "conjunction" }).format(missing)} ${missing.length === 1 ? "is" : "are"} not set, so the backend tests cannot create their test database and storage container. Set ${missing.length === 1 ? "it" : "them"} (see docs/guides/testing.md) or pass --skip-backend.`,
    );
    process.exit(1);
  }

  rmSync(join(backend, "artifacts", "TestResults"), { recursive: true, force: true });
  steps.push(
    { name: "backend restore (locked)", cwd: backend, command: "dotnet", args: ["restore", "--locked-mode"] },
    { name: "backend build", cwd: backend, command: "dotnet", args: ["build", "--no-restore", "-warnaserror"] },
    { name: "backend format", cwd: backend, command: "dotnet", args: ["format", "--verify-no-changes", "--no-restore"] },
    { name: "pending model changes", cwd: repoRoot, command: "node", args: ["scripts/ef/ef.ts", "pending", "--all", "--no-build"] },
    {
      name: "backend tests",
      cwd: backend,
      command: "dotnet",
      args: ["test", "--solution", "Dewiride.Erp.slnx", "--no-build", "--report-trx", "--coverage", "--coverage-output-format", "cobertura"],
    },
    { name: "coverage gate", cwd: repoRoot, command: "node", args: ["scripts/checks/coverage-gate.ts"] },
  );
}

steps.push(
  { name: "folder cap", cwd: repoRoot, command: "node", args: ["scripts/checks/folder-size.ts"] },
  { name: "comment policy", cwd: repoRoot, command: "node", args: ["scripts/checks/comment-policy.ts"] },
  { name: "secret patterns", cwd: repoRoot, command: "node", args: ["scripts/checks/secret-patterns.ts"] },
  { name: "azure scripts check", cwd: repoRoot, command: bash, args: ["scripts/azure/check.sh"], shell: false },
  { name: "roadmap tests", cwd: repoRoot, command: "node", args: ["--test", "scripts/roadmap/tests/*.test.ts"] },
  { name: "check tests", cwd: repoRoot, command: "node", args: ["--test", "scripts/checks/tests/*.test.ts"] },
  { name: "api client tests", cwd: repoRoot, command: "node", args: ["--test", "scripts/api-client/tests/*.test.ts"] },
  { name: "ef script tests", cwd: repoRoot, command: "node", args: ["--test", "scripts/ef/tests/*.test.ts"] },
  { name: "roadmap check", cwd: repoRoot, command: "node", args: ["scripts/roadmap/roadmap.ts", "check"] },
);

if (!values["skip-frontend"]) {
  steps.push(
    { name: "frontend install (frozen)", cwd: frontend, command: "pnpm", args: ["install", "--frozen-lockfile"] },
    { name: "api client drift", cwd: repoRoot, command: "node", args: ["scripts/api-client/drift.ts"] },
    { name: "frontend lint", cwd: frontend, command: "pnpm", args: ["lint"] },
    { name: "frontend typecheck", cwd: frontend, command: "pnpm", args: ["typecheck"] },
    { name: "frontend unit tests", cwd: frontend, command: "pnpm", args: ["test:unit"] },
    { name: "frontend format", cwd: frontend, command: "pnpm", args: ["format:check"] },
    { name: "feature boundaries", cwd: repoRoot, command: "node", args: ["scripts/checks/feature-boundaries.ts"] },
    { name: "frontend build", cwd: frontend, command: "pnpm", args: ["build"] },
    { name: "web startup guard", cwd: repoRoot, command: "node", args: ["scripts/checks/web-startup-guard.ts"] },
  );
}

if (values.e2e) {
  steps.push({ name: "playwright", cwd: frontend, command: "pnpm", args: ["e2e"] });
}

if (values.docker) {
  const compose = join(repoRoot, "infra", "compose");
  steps.push(
    { name: "docker build api", cwd: repoRoot, command: "docker", args: ["build", "-f", "infra/docker/api.Dockerfile", "-t", "ghcr.io/dewiride-open-source/erp-ai-pro/api:verify", "."] },
    { name: "docker build web", cwd: repoRoot, command: "docker", args: ["build", "-f", "infra/docker/web.Dockerfile", "-t", "ghcr.io/dewiride-open-source/erp-ai-pro/web:verify", "."] },
    { name: "compose config (local)", cwd: compose, command: "docker", args: ["compose", "-f", "compose.yaml", "-f", "compose.override.yaml", "config", "--quiet"] },
    { name: "compose config (production)", cwd: compose, command: "docker", args: ["compose", "-f", "compose.yaml", "-f", "compose.production.yaml", "config", "--quiet"], env: { IMAGE_TAG: "verify" } },
    { name: "compose smoke", cwd: repoRoot, command: "node", args: ["scripts/checks/compose-smoke.ts"], env: { IMAGE_TAG: "verify" } },
  );
}

const results: { name: string; ok: boolean; seconds: number }[] = [];

for (const step of steps) {
  if (!existsSync(step.cwd)) {
    console.error(`\n✖ ${step.name}: directory ${step.cwd} does not exist`);
    results.push({ name: step.name, ok: false, seconds: 0 });
    break;
  }
  console.log(`\n▶ ${step.name}`);
  const started = performance.now();
  const result = spawnSync(step.command, step.args, {
    cwd: step.cwd,
    stdio: "inherit",
    shell: step.shell ?? isWindows,
    env: { ...process.env, ...step.env },
  });
  const seconds = (performance.now() - started) / 1000;
  const ok = result.status === 0;
  results.push({ name: step.name, ok, seconds });
  console.log(`${ok ? "✔" : "✖"} ${step.name} (${seconds.toFixed(1)}s)`);
  if (!ok) break;
}

console.log("\nSummary");
for (const r of results) console.log(`  ${r.ok ? "✔" : "✖"} ${r.name.padEnd(30)} ${r.seconds.toFixed(1)}s`);
const failed = results.some((r) => !r.ok);
const skipped = steps.length - results.length;
if (skipped > 0) console.log(`  ${skipped} step(s) not run`);
process.exit(failed ? 1 : 0);

function gitBashPath(): string {
  const git = spawnSync("where", ["git"], { encoding: "utf8" });
  const gitExe = git.status === 0 ? git.stdout.split(/\r?\n/).find((line) => line.endsWith("git.exe")) : undefined;
  if (!gitExe) return "bash";
  let directory = dirname(gitExe);
  for (let depth = 0; depth < 4; depth++) {
    for (const candidate of [join(directory, "bin", "bash.exe"), join(directory, "usr", "bin", "bash.exe")]) {
      if (existsSync(candidate)) return candidate;
    }
    directory = dirname(directory);
  }
  return "bash";
}
