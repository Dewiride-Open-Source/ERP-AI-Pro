import { spawnSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { basename, join, resolve } from "node:path";
import { parseArgs } from "node:util";

import { isWithinPolicy, lockFilePackages, nugetPackageLicence, nuspecLicence, type PackageLicence, pnpmPackageLicences } from "./lib/licence-policy.ts";
import { repoRoot, walk } from "./lib/walk.ts";

const usage = `Usage: node scripts/checks/licences.ts <npm|nuget> [options]

  npm      every package pnpm installed for the frontend workspace (pnpm licenses list --json --recursive)
  nuget    every package in the backend/Hosts/**/packages.lock.json closure, read from its .nuspec

Options: --root <dir> (repository root), --from <file> (a saved pnpm licences listing), --packages <dir> (NuGet global packages folder).`;

const { positionals, values } = parseArgs({
  allowPositionals: true,
  options: {
    root: { type: "string" },
    from: { type: "string" },
    packages: { type: "string" },
    help: { type: "boolean", default: false },
  },
});

const [ecosystem] = positionals;
if (values.help || (ecosystem !== "npm" && ecosystem !== "nuget") || positionals.length > 1) {
  console.log(usage);
  process.exit(values.help ? 0 : 64);
}

const root = values.root === undefined ? repoRoot : resolve(values.root);

function fail(message: string): never {
  console.error(message);
  process.exit(1);
}

function npmLicences(): PackageLicence[] {
  if (values.from !== undefined) return pnpmPackageLicences(readFileSync(resolve(values.from), "utf8"));
  const result = spawnSync("pnpm licenses list --json --recursive", { cwd: join(root, "frontend"), encoding: "utf8", maxBuffer: 64 * 1024 * 1024, shell: true });
  if (result.status !== 0) fail(`pnpm licenses list failed; run "pnpm install --frozen-lockfile" in frontend first\n${result.stderr ?? ""}`);
  return pnpmPackageLicences(result.stdout);
}

function globalPackagesFolder(): string {
  if (values.packages !== undefined) return resolve(values.packages);
  const result = spawnSync("dotnet", ["nuget", "locals", "global-packages", "--list"], { cwd: join(root, "backend"), encoding: "utf8" });
  const folder = /global-packages:\s*(.+)/.exec(result.stdout ?? "")?.[1]?.trim();
  if (result.status !== 0 || !folder) fail(`dotnet nuget locals global-packages --list failed\n${result.stderr ?? ""}`);
  return folder;
}

function nugetLicences(): PackageLicence[] {
  const folder = globalPackagesFolder();
  const lockFiles = walk(join(root, "backend", "Hosts"), (file) => basename(file) === "packages.lock.json");
  if (lockFiles.length === 0) fail("no packages.lock.json under backend/Hosts");
  const packages = new Map<string, { name: string; version: string }>();
  for (const lockFile of lockFiles) {
    for (const locked of lockFilePackages(readFileSync(lockFile, "utf8"))) packages.set(`${locked.name}@${locked.version}`.toLowerCase(), locked);
  }
  const missing: string[] = [];
  const licences: PackageLicence[] = [];
  for (const { name, version } of packages.values()) {
    const nuspec = join(folder, name.toLowerCase(), version.toLowerCase(), `${name.toLowerCase()}.nuspec`);
    if (!existsSync(nuspec)) missing.push(`  ${name} ${version}`);
    else licences.push(nugetPackageLicence(name, version, nuspecLicence(readFileSync(nuspec, "utf8"))));
  }
  if (missing.length > 0) fail(`Packages missing from ${folder}; run "dotnet restore --locked-mode" in backend first:\n${missing.join("\n")}`);
  return licences;
}

const licences = ecosystem === "npm" ? npmLicences() : nugetLicences();
const outside = licences.filter((entry) => !isWithinPolicy(entry)).sort((a, b) => a.name.localeCompare(b.name) || a.version.localeCompare(b.version));

if (outside.length > 0) {
  console.error(`${ecosystem} packages outside the licence policy (scripts/checks/lib/licence-policy.ts and .github/dependency-review-config.yml):`);
  for (const entry of outside) console.error(`  ${entry.name} ${entry.version}: ${entry.licence}`);
  process.exit(1);
}

console.log(`licences ok: ${licences.length} ${ecosystem} packages`);
