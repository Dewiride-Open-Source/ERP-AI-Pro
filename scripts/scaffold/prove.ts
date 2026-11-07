import { spawnSync } from "node:child_process";
import { existsSync, rmSync } from "node:fs";
import { connect } from "node:net";
import { dirname, join, resolve } from "node:path";
import { parseArgs } from "node:util";
import { fileURLToPath, pathToFileURL } from "node:url";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
const isWindows = process.platform === "win32";
const backendTestVariables = ["ERP_TEST_SQL_CONNECTION", "ERP_TEST_BLOB_EMULATOR_HOST"];
const worktree = resolve(repoRoot, "..", "ERP-AI-Pro-scaffold-proof");
const specsStep = "proof module specs";

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

type E2eTargets = {
  readonly startServers: boolean;
  readonly apiBaseURL: string;
  readonly gatedApiBaseURL: string;
  readonly baseURL: string;
  readonly offlineBaseURL?: string;
  readonly gatedBaseURL?: string;
  readonly webPorts: Readonly<Record<string, number>>;
};

const { values } = parseArgs({
  options: {
    "in-place": { type: "boolean", default: false },
    e2e: { type: "boolean", default: false },
    "all-projects": { type: "boolean", default: false },
    project: { type: "string", multiple: true },
    help: { type: "boolean", default: false },
  },
});

if (values.help) {
  console.log("Usage: node scripts/scaffold/prove.ts [--e2e [--all-projects | --project <name>…]] [--in-place]");
  console.log("Generates the throwaway module ScaffoldProof/Jobs and runs every check over it, in a worktree of HEAD beside the repository,");
  console.log("or, on a GitHub Actions runner alone, in place with --in-place. --e2e adds the generated specs, on Chromium unless");
  console.log("--all-projects or one --project per Playwright project names others.");
  process.exit(0);
}

const projectChoices = values.project ?? [];
if (values["in-place"] && process.env.GITHUB_ACTIONS !== "true") {
  console.error("✖ --in-place would generate the proof module into this checkout; it runs on a GitHub Actions runner only.");
  process.exit(1);
}

if ((values["all-projects"] || projectChoices.length > 0) && !values.e2e) {
  console.error("✖ --all-projects and --project choose where the generated specs run, so they need --e2e.");
  process.exit(1);
}

if (values["all-projects"] && projectChoices.length > 0) {
  console.error("✖ --all-projects runs every Playwright project; leave out --project, or name the projects without --all-projects.");
  process.exit(1);
}

const missing = backendTestVariables.filter((name) => !process.env[name]);
if (missing.length > 0) {
  console.error(`✖ ${missing.join(" and ")} must be set: the proof migrates the module and runs its tests against SQL Server and Azurite.`);
  process.exit(1);
}

type Step = { readonly name: string; readonly cwd: string; readonly command: string; readonly args: readonly string[]; readonly shell?: boolean };

const inPlace = values["in-place"];
const root = inPlace ? repoRoot : worktree;
const backend = join(root, "backend");
const frontend = join(root, "frontend");
const ciBuild = process.env.GITHUB_ACTIONS === "true" ? ["-p:ContinuousIntegrationBuild=true"] : [];
const chosenProjects = projectChoices.length > 0 ? projectChoices : ["chromium"];
const projects = values["all-projects"] ? [] : chosenProjects.map((name) => `--project=${name}`);

const results: { name: string; ok: boolean; seconds: number }[] = [];
let proved = "the checkout";

if (!inPlace) {
  if (existsSync(worktree)) {
    console.error(`✖ ${worktree} exists; if no other proof is running, delete that folder and run git worktree prune`);
    process.exit(1);
  }

  proved = `HEAD ${git(["rev-parse", "--short", "HEAD"]).trim()}`;
  const outside = git(["status", "--porcelain=v1", "--untracked-files=all"]).split("\n").filter((line) => line !== "");
  if (outside.length > 0) {
    console.warn(`⚠ the proof checks ${proved}; these changes of the checkout are not part of it:`);
    for (const line of outside) console.warn(`  ${line}`);
  }

  if (!run({ name: "worktree at HEAD", cwd: repoRoot, command: "git", args: ["worktree", "add", "--detach", worktree, "HEAD"], shell: false })) finish();
}

try {
  const steps: Step[] = [
    { name: "scaffold tests", cwd: root, command: "node", args: ["--test", "scripts/scaffold/tests/*.test.ts"], shell: false },
    { name: "backend tools restore", cwd: backend, command: "dotnet", args: ["tool", "restore"], shell: false },
    { name: "frontend install (frozen)", cwd: frontend, command: "pnpm", args: ["install", "--frozen-lockfile"] },
    { name: "generate the proof module", cwd: root, command: "node", args: ["scripts/scaffold/new-module.ts", ...proof.arguments], shell: false },
    { name: "registration audit", cwd: root, command: "node", args: ["--test", "scripts/scaffold/tests/audit.test.ts"], shell: false },
    { name: "backend restore (locked)", cwd: backend, command: "dotnet", args: ["restore", "--locked-mode"], shell: false },
    { name: "backend build", cwd: backend, command: "dotnet", args: ["build", "--no-restore", "-warnaserror", ...ciBuild], shell: false },
    { name: "pending model changes", cwd: root, command: "node", args: ["scripts/ef/ef.ts", "pending", "--context", proof.key, "--no-build"], shell: false },
    { name: "backend format", cwd: backend, command: "dotnet", args: ["format", "--verify-no-changes", "--no-restore", "--include", "Modules/ScaffoldProof/", "Hosts/Composition/"], shell: false },
    testStep("proof module tests", `${proof.folder}/Tests/UnitTests/${proof.assembly}.UnitTests/${proof.assembly}.UnitTests.csproj`, "unit", true),
    testStep("proof module integration tests", `${proof.folder}/Tests/IntegrationTests/${proof.assembly}.IntegrationTests/${proof.assembly}.IntegrationTests.csproj`, "integration", true),
    testStep("architecture tests", "Tests/Architecture/Dewiride.Erp.ArchitectureTests/Dewiride.Erp.ArchitectureTests.csproj", "architecture", false),
    testStep(
      "API host tests over every module",
      "Tests/Host/Dewiride.Erp.Host.Api.IntegrationTests/Dewiride.Erp.Host.Api.IntegrationTests.csproj",
      "host-api",
      false,
      hostApiTestClasses.flatMap((name) => ["--filter-class", `*${name}`]),
    ),
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

  if (values.e2e) steps.push({ name: specsStep, cwd: frontend, command: "pnpm", args: ["e2e", proof.specs, ...projects] });

  for (const step of steps) {
    if (step.name === specsStep) {
      const busy = await busyServerAddresses();
      if (busy.length > 0) {
        console.error(`✖ ${busy.join(", ")} already answer, so Playwright would test those servers instead of the proof's; stop them and run again`);
        results.push({ name: step.name, ok: false, seconds: 0 });
        break;
      }
    }

    if (!run(step)) break;
  }
} finally {
  if (!inPlace && existsSync(worktree)) removeWorktree();
}

finish();

function testStep(name: string, project: string, report: string, coverage: boolean, extra: readonly string[] = []): Step {
  const coverageArguments = coverage ? ["--coverage", "--coverage-output-format", "cobertura", "--coverage-output", `scaffold-proof-${report}.cobertura.xml`] : [];
  return {
    name,
    cwd: backend,
    command: "dotnet",
    args: ["test", "--project", project, "--no-build", "--report-trx", "--report-trx-filename", `scaffold-proof-${report}.trx`, ...coverageArguments, ...extra],
    shell: false,
  };
}

async function busyServerAddresses(): Promise<string[]> {
  const targets = (await import(pathToFileURL(join(frontend, "e2e", "fixtures", "targets.ts")).href)) as E2eTargets;
  if (!targets.startServers) return [];
  const addresses = [targets.apiBaseURL, targets.gatedApiBaseURL, targets.baseURL, targets.offlineBaseURL, targets.gatedBaseURL, ...Object.values(targets.webPorts).map((port) => `http://127.0.0.1:${port}`)]
    .filter((address): address is string => address !== undefined)
    .map((address) => new URL(address));
  const answers = await Promise.all(addresses.map(async (url) => ((await accepts(url.hostname, Number(url.port))) ? url.host : undefined)));
  return answers.filter((host): host is string => host !== undefined);
}

function accepts(host: string, port: number): Promise<boolean> {
  return new Promise((settle) => {
    const socket = connect({ host, port });
    socket.setTimeout(1000);
    socket.once("connect", () => {
      socket.destroy();
      settle(true);
    });
    socket.once("timeout", () => {
      socket.destroy();
      settle(false);
    });
    socket.once("error", () => settle(false));
  });
}

function removeWorktree(): void {
  spawnSync("dotnet", ["build-server", "shutdown"], { cwd: join(worktree, "backend"), stdio: "ignore" });
  spawnSync("git", ["worktree", "remove", "--force", worktree], { cwd: repoRoot, stdio: "ignore" });
  try {
    if (existsSync(worktree)) rmSync(worktree, { recursive: true, force: true, maxRetries: 10, retryDelay: 500 });
  } catch (error) {
    console.error(`✖ the proof worktree could not be removed: ${(error as Error).message}`);
  }

  spawnSync("git", ["worktree", "prune"], { cwd: repoRoot, stdio: "ignore" });
  if (existsSync(worktree)) console.error(`✖ delete ${worktree} once nothing holds its files, then run git worktree prune`);
}

function git(args: readonly string[]): string {
  const result = spawnSync("git", args, { cwd: repoRoot, encoding: "utf8", shell: false });
  if (result.error !== undefined || result.status !== 0) {
    console.error(`✖ git ${args.join(" ")} failed: ${(result.stderr ?? "").trim() || result.error?.message}`);
    process.exit(1);
  }

  return result.stdout;
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

function finish(): never {
  console.log(`\nSummary of the proof of ${proved}`);
  for (const result of results) console.log(`  ${result.ok ? "✔" : "✖"} ${result.name.padEnd(34)} ${result.seconds.toFixed(1)}s`);
  process.exit(results.length > 0 && results.every((result) => result.ok) ? 0 : 1);
}
