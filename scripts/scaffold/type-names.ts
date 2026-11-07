import { spawnSync } from "node:child_process";
import { existsSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { parseArgs } from "node:util";
import { fileURLToPath } from "node:url";

import { dotnetTypeNamesFile, typeNamespacesOf } from "./lib/type-names.ts";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
const backend = join(repoRoot, "backend");
const templates = join(repoRoot, "scripts", "scaffold", "templates");
const referencePacks = ["Microsoft.NETCore.App.Ref", "Microsoft.AspNetCore.App.Ref"] as const;
const typeMember = /<member name="T:([A-Za-z_][A-Za-z0-9_.]*)"/g;
const documentationFile = /^(?:lib|ref)\/[^/]+\/[^/]+\.xml$/i;

const { values } = parseArgs({ options: { help: { type: "boolean", default: false } } });
if (values.help) {
  console.log("Usage: node scripts/scaffold/type-names.ts");
  console.log(`Rewrites ${relative(repoRoot, dotnetTypeNamesFile)}: the public non-generic types of every namespace a generated C# file that`);
  console.log("names the entity imports, read from the documentation of the SDK's reference packs and of the restored packages.");
  console.log("Run it after a .NET or package upgrade and whenever such a template imports another namespace; the backend must be restored.");
  process.exit(0);
}

const names = new Map(typeNamespacesOf(templates).map((namespace) => [namespace, new Set<string>()]));
for (const file of [...referencePackFiles(), ...packageFiles()]) {
  for (const [, fullName] of readFileSync(file, "utf8").matchAll(typeMember)) {
    const dot = (fullName as string).lastIndexOf(".");
    names.get((fullName as string).slice(0, dot))?.add((fullName as string).slice(dot + 1));
  }
}

const missing = [...names].filter(([, found]) => found.size === 0).map(([namespace]) => namespace);
if (missing.length > 0) fail(`no documentation names a type of ${missing.join(", ")}; restore the backend (cd backend && dotnet restore) and run again`);
const document = Object.fromEntries([...names].map(([namespace, found]) => [namespace, [...found].sort()]));
writeFileSync(dotnetTypeNamesFile, `${JSON.stringify(document, null, 2)}\n`);
console.log(`✔ ${relative(repoRoot, dotnetTypeNamesFile)}: ${[...names.values()].reduce((total, found) => total + found.size, 0)} type names in ${names.size} namespaces`);

function referencePackFiles(): string[] {
  const sdk = command("dotnet", ["--version"]).trim();
  const listed = command("dotnet", ["--list-sdks"]).split(/\r?\n/).find((line) => line.startsWith(`${sdk} [`));
  const sdkFolder = /\[(.+)\]\s*$/.exec(listed ?? "")?.[1];
  if (sdkFolder === undefined) fail(`dotnet --list-sdks does not list the SDK ${sdk} that ${relative(repoRoot, backend)} selects`);
  const bundled = readFileSync(join(sdkFolder, sdk, "Microsoft.NETCoreSdk.BundledVersions.props"), "utf8");
  const runtime = /<BundledNETCoreAppPackageVersion>([^<]+)</.exec(bundled)?.[1];
  if (runtime === undefined) fail(`the SDK ${sdk} names no bundled runtime version`);
  const files: string[] = [];
  for (const pack of referencePacks) {
    const reference = join(dirname(sdkFolder), "packs", pack, runtime, "ref");
    if (!existsSync(reference)) fail(`${reference} does not exist; install the .NET ${runtime} targeting packs`);
    for (const framework of readdirSync(reference)) {
      for (const name of readdirSync(join(reference, framework))) if (name.endsWith(".xml")) files.push(join(reference, framework, name));
    }
  }

  return files;
}

function packageFiles(): string[] {
  const restored = join(backend, "artifacts", "obj");
  if (!existsSync(restored)) fail(`${relative(repoRoot, restored)} does not exist; restore the backend first (cd backend && dotnet restore)`);
  const files = new Set<string>();
  for (const project of readdirSync(restored)) {
    const assets = join(restored, project, "project.assets.json");
    if (!existsSync(assets)) continue;
    const parsed = JSON.parse(readFileSync(assets, "utf8")) as {
      readonly packageFolders?: Record<string, unknown>;
      readonly libraries?: Record<string, { readonly type?: string; readonly path?: string; readonly files?: readonly string[] }>;
    };
    const folder = Object.keys(parsed.packageFolders ?? {})[0];
    if (folder === undefined) continue;
    for (const library of Object.values(parsed.libraries ?? {})) {
      if (library.type !== "package" || library.path === undefined) continue;
      for (const file of library.files ?? []) if (documentationFile.test(file)) files.add(join(folder, library.path, file));
    }
  }

  return [...files].filter((file) => existsSync(file));
}

function command(file: string, args: readonly string[]): string {
  const result = spawnSync(file, args, { cwd: backend, encoding: "utf8", shell: false });
  if (result.error !== undefined || result.status !== 0) fail(`${file} ${args.join(" ")} failed: ${(result.stderr ?? "").trim() || result.error?.message}`);
  return result.stdout;
}

function fail(message: string): never {
  console.error(`✖ ${message}`);
  process.exit(1);
}
