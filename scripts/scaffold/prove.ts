import { spawnSync } from "node:child_process";
import { existsSync, rmSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { parseArgs } from "node:util";
import { fileURLToPath } from "node:url";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
const isWindows = process.platform === "win32";
const backendTestVariables = ["ERP_TEST_SQL_CONNECTION", "ERP_TEST_BLOB_EMULATOR_HOST"];
const worktree = resolve(repoRoot, "..", "ERP-AI-Pro-scaffold-proof");

const proof = {
  module: "ScaffoldProof/Jobs",
  key: "scaffold-proof-jobs",
  folder: "Modules/ScaffoldProof/Jobs",
  assembly: "Dewiride.Erp.Modules.ScaffoldProof.Jobs",
  specs: "tests/scaffold-proof/jobs",
  arguments: [
    "ScaffoldProof/Jobs",
    "--entity",
    "LedgerNote",
    "--title",
    "Ledger notes",
    "--description",
    "Keep short notes about the ledgers of the scaffold proof.",
    "--read-permission",
    "See the ledger notes of the scaffold proof.",
    "--create-permission",
    "Add ledger notes to the scaffold proof.",
    "--area-title",
    "Scaffold proof",
    "--icon",
    "NotebookIcon",
  ],
};

const hostApiTestClasses = ["OpenApiSnapshotTests", "OpenApiSchemaShapeTests", "OpenApiSecurityTests", "OpenApiDocumentTests", "FeatureEndpointsTests", "ConfigurationSourceTests"];

const { values } = parseArgs({
  options: {
    "in-place": { type: "boolean", default: false },
    e2e: { type: "boolean", default: false },
    "all-projects": { type: "boolean", default: false },
    help: { type: "boolean", default: false },
  },
});

if (values.help) {
  console.log("Usage: node scripts/scaffold/prove.ts [--e2e [--all-projects]] [--in-place]");
  console.log("Generates the throwaway module ScaffoldProof/Jobs and runs every check over it, in a worktree of HEAD beside the repository,");
  console.log("or, on a GitHub Actions runner alone, in place with --in-place.");
  process.exit(0);
}

if (values["in-place"] && process.env.GITHUB_ACTIONS !== "true") {
  console.error("✖ --in-place would generate the proof module into this checkout; it runs on a GitHub Actions runner only.");
  process.exit(1);
}

const missing = backendTestVariables.filter((name) => !process.env[name]);
if (missing.length > 0) {
  console.error(`✖ ${missing.join(" and ")} must be set: the proof migrates the module and runs its tests against SQL Server and Azurite.`);
  process.exit(1);
}

type Step = { readonly name: string; readonly cwd: string; readonly command: string; readonly args: readonly string[]; readonly shell?: boolean };

const root = values["in-place"] ? repoRoot : worktree;
const backend = join(root, "backend");
const frontend = join(root, "frontend");
const ciBuild = process.env.GITHUB_ACTIONS === "true" ? ["-p:ContinuousIntegrationBuild=true"] : [];
const projects = values["all-projects"] ? [] : ["--project=chromium"];

const results: { name: string; ok: boolean; seconds: number }[] = [];

if (!values["in-place"]) {
  if (existsSync(worktree)) {
    console.error(`✖ ${worktree} exists; remove it with: git worktree remove --force "${worktree}"`);
    process.exit(1);
  }

  run({ name: "worktree at HEAD", cwd: repoRoot, command: "git", args: ["worktree", "add", "--detach", worktree, "HEAD"], shell: false });
}

try {
  const steps: Step[] = [
    { name: "scaffold tests", cwd: root, command: "node", args: ["--test", "scripts/scaffold/tests/*.test.ts"], shell: false },
    { name: "backend tools restore", cwd: backend, command: "dotnet", args: ["tool", "restore"], shell: false },
    { name: "frontend install (frozen)", cwd: frontend, command: "pnpm", args: ["install", "--frozen-lockfile"] },
    { name: "generate the proof module", cwd: root, command: "node", args: ["scripts/scaffold/new-module.ts", ...proof.arguments], shell: false },
    { name: "backend restore (locked)", cwd: backend, command: "dotnet", args: ["restore", "--locked-mode"], shell: false },
    { name: "backend build", cwd: backend, command: "dotnet", args: ["build", "--no-restore", "-warnaserror", ...ciBuild], shell: false },
    { name: "pending model changes", cwd: root, command: "node", args: ["scripts/ef/ef.ts", "pending", "--context", proof.key, "--no-build"], shell: false },
    { name: "backend format", cwd: backend, command: "dotnet", args: ["format", "--verify-no-changes", "--no-restore", "--include", "Modules/ScaffoldProof/", "Hosts/Composition/"], shell: false },
    { name: "proof module tests", cwd: backend, command: "dotnet", args: ["test", "--project", `${proof.folder}/Tests/UnitTests/${proof.assembly}.UnitTests/${proof.assembly}.UnitTests.csproj`, "--no-build", "--coverage", "--coverage-output-format", "cobertura"], shell: false },
    { name: "proof module integration tests", cwd: backend, command: "dotnet", args: ["test", "--project", `${proof.folder}/Tests/IntegrationTests/${proof.assembly}.IntegrationTests/${proof.assembly}.IntegrationTests.csproj`, "--no-build", "--coverage", "--coverage-output-format", "cobertura"], shell: false },
    { name: "architecture tests", cwd: backend, command: "dotnet", args: ["test", "--project", "Tests/Architecture/Dewiride.Erp.ArchitectureTests/Dewiride.Erp.ArchitectureTests.csproj", "--no-build"], shell: false },
    {
      name: "API host tests over every module",
      cwd: backend,
      command: "dotnet",
      args: ["test", "--project", "Tests/Host/Dewiride.Erp.Host.Api.IntegrationTests/Dewiride.Erp.Host.Api.IntegrationTests.csproj", "--no-build", ...hostApiTestClasses.flatMap((name) => ["--filter-class", `*${name}`])],
      shell: false,
    },
    { name: "proof module coverage", cwd: root, command: "node", args: ["scripts/checks/coverage-gate.ts", "--module", proof.module], shell: false },
    { name: "folder cap", cwd: root, command: "node", args: ["scripts/checks/folder-size.ts"], shell: false },
    { name: "comment policy", cwd: root, command: "node", args: ["scripts/checks/comment-policy.ts"], shell: false },
    { name: "secret patterns", cwd: root, command: "node", args: ["scripts/checks/secret-patterns.ts"], shell: false },
    { name: "frontend lint", cwd: frontend, command: "pnpm", args: ["lint"] },
    { name: "frontend typecheck", cwd: frontend, command: "pnpm", args: ["typecheck"] },
    { name: "frontend unit tests", cwd: frontend, command: "pnpm", args: ["test:unit"] },
    { name: "frontend format", cwd: frontend, command: "pnpm", args: ["format:check"] },
    { name: "feature boundaries", cwd: root, command: "node", args: ["scripts/checks/feature-boundaries.ts"], shell: false },
    { name: "frontend build", cwd: frontend, command: "pnpm", args: ["build"] },
  ];

  if (values.e2e) steps.push({ name: "proof module specs", cwd: frontend, command: "pnpm", args: ["e2e", proof.specs, ...projects] });

  for (const step of steps) if (!run(step)) break;
} finally {
  if (!values["in-place"] && existsSync(worktree)) removeWorktree();
}

console.log("\nSummary");
for (const result of results) console.log(`  ${result.ok ? "✔" : "✖"} ${result.name.padEnd(34)} ${result.seconds.toFixed(1)}s`);
process.exit(results.every((result) => result.ok) ? 0 : 1);

function removeWorktree(): void {
  spawnSync("dotnet", ["build-server", "shutdown"], { cwd: join(worktree, "backend"), stdio: "ignore" });
  spawnSync("git", ["worktree", "remove", "--force", worktree], { cwd: repoRoot, stdio: "ignore" });
  if (existsSync(worktree)) rmSync(worktree, { recursive: true, force: true, maxRetries: 10, retryDelay: 500 });
  spawnSync("git", ["worktree", "prune"], { cwd: repoRoot, stdio: "ignore" });
  if (existsSync(worktree)) console.error(`✖ remove the proof worktree with: git worktree remove --force "${worktree}"`);
}

function run(step: Step): boolean {
  console.log(`\n▶ ${step.name}`);
  const started = performance.now();
  const result = spawnSync(step.command, step.args, { cwd: step.cwd, stdio: "inherit", shell: step.shell ?? isWindows, env: process.env });
  const seconds = (performance.now() - started) / 1000;
  const ok = result.status === 0;
  results.push({ name: step.name, ok, seconds });
  console.log(`${ok ? "✔" : "✖"} ${step.name} (${seconds.toFixed(1)}s)`);
  return ok;
}
