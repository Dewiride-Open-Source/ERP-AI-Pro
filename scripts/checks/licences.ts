import { spawnSync } from "node:child_process";
import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { join, resolve } from "node:path";
import { parseArgs } from "node:util";

import {
  assetsFilePackages,
  nugetPackageLicence,
  nuspecLicence,
  type PackageLicence,
  pnpmPackageLicences,
  policyFinding,
  type RestoredPackage,
  toolManifestPackages,
} from "./lib/licence-policy.ts";
import { repoRoot } from "./lib/walk.ts";

const usage = `Usage: node scripts/checks/licences.ts <npm|nuget> [options]

  npm      every package pnpm installed for the frontend workspace (pnpm licenses list --json --recursive)
  nuget    every package restored for any backend project (backend/artifacts/obj/*/project.assets.json) and every
           local tool in backend/.config/dotnet-tools.json, read from its .nuspec

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

function packageEntry(directory: string, relativePath: string): string | undefined {
  let current = directory;
  for (const segment of relativePath.split(/[\\/]/).filter((part) => part.length > 0)) {
    if (!existsSync(current) || !statSync(current).isDirectory()) return undefined;
    const entry = readdirSync(current).find((candidate) => candidate.toLowerCase() === segment.toLowerCase());
    if (entry === undefined) return undefined;
    current = join(current, entry);
  }
  return statSync(current).isFile() ? current : undefined;
}

function restoredPackages(): RestoredPackage[] {
  const objects = join(root, "backend", "artifacts", "obj");
  const assetsFiles = existsSync(objects)
    ? readdirSync(objects, { withFileTypes: true })
        .filter((entry) => entry.isDirectory())
        .map((entry) => join(objects, entry.name, "project.assets.json"))
        .filter((file) => existsSync(file))
    : [];
  if (assetsFiles.length === 0) fail(`no project.assets.json under ${objects}; run "dotnet restore --locked-mode" in backend first`);
  const manifest = join(root, "backend", ".config", "dotnet-tools.json");
  if (!existsSync(manifest)) fail(`${manifest} is missing`);
  return [...assetsFiles.flatMap((file) => assetsFilePackages(readFileSync(file, "utf8"))), ...toolManifestPackages(readFileSync(manifest, "utf8"))];
}

function nugetLicences(): PackageLicence[] {
  const folder = globalPackagesFolder();
  const packages = new Map<string, RestoredPackage>();
  for (const restored of restoredPackages()) packages.set(`${restored.name}@${restored.version}`.toLowerCase(), restored);
  const missing: string[] = [];
  const licences: PackageLicence[] = [];
  for (const { name, version } of packages.values()) {
    const directory = join(folder, name.toLowerCase(), version.toLowerCase());
    const nuspec = packageEntry(directory, `${name}.nuspec`);
    if (nuspec === undefined) {
      missing.push(`  ${name} ${version}`);
      continue;
    }
    const licence = nuspecLicence(readFileSync(nuspec, "utf8"));
    const licenceFile = licence.kind === "file" ? packageEntry(directory, licence.file) : undefined;
    licences.push(nugetPackageLicence(name, version, licence, licenceFile === undefined ? undefined : readFileSync(licenceFile)));
  }
  if (missing.length > 0) fail(`Packages missing from ${folder}; run "dotnet tool restore" and "dotnet restore --locked-mode" in backend first:\n${missing.join("\n")}`);
  return licences;
}

const licences = ecosystem === "npm" ? npmLicences() : nugetLicences();
const outside = licences
  .flatMap((entry) => {
    const finding = policyFinding(entry);
    return finding === undefined ? [] : [{ entry, finding }];
  })
  .sort((a, b) => a.entry.name.localeCompare(b.entry.name) || a.entry.version.localeCompare(b.entry.version));

if (outside.length > 0) {
  console.error(`${ecosystem} packages outside the licence policy (scripts/checks/lib/licence-policy.ts and .github/dependency-review-config.yml):`);
  for (const { entry, finding } of outside) console.error(`  ${entry.name} ${entry.version}: ${finding}`);
  process.exit(1);
}

console.log(`licences ok: ${licences.length} ${ecosystem} packages`);
