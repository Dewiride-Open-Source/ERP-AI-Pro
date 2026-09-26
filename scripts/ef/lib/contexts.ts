import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { dirname, join, posix, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

export const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
export const backendRoot = join(repoRoot, "backend");

export type DbContextEntry = {
  key: string;
  contextName: string;
  project: string;
};

const searchRoots = ["Modules", "BuildingBlocks"];
const skippedDirectories = new Set(["bin", "obj", "artifacts", "Tests", "node_modules"]);
const contextDeclaration = /\bclass\s+(\w+DbContext)\b[^{]*:\s*ModuleDbContext\b/;

// Every catalogue context derives from ModuleDbContext (the AddModuleDbContext constraint) and sits in a Persistence folder
// (an architecture rule for modules, the convention for building blocks), so the source tree alone names every context and
// the project that owns its migrations.
export function discoverContexts(backend: string = backendRoot): DbContextEntry[] {
  const entries: DbContextEntry[] = [];
  for (const root of searchRoots) {
    const directory = join(backend, root);
    if (existsSync(directory)) collect(directory, backend, entries);
  }

  const duplicate = entries.find((entry, index) => entries.findIndex((other) => other.key === entry.key) !== index);
  if (duplicate) throw new Error(`two DbContexts resolve to the key "${duplicate.key}"; rename one of them.`);

  return entries.sort((left, right) => left.key.localeCompare(right.key));
}

export function contextKey(contextName: string): string {
  return contextName
    .replace(/DbContext$/, "")
    .replace(/([a-z0-9])([A-Z])/g, "$1-$2")
    .toLowerCase();
}

function collect(directory: string, backend: string, entries: DbContextEntry[]): void {
  for (const name of readdirSync(directory)) {
    if (skippedDirectories.has(name)) continue;
    const path = join(directory, name);
    if (statSync(path).isDirectory()) {
      collect(path, backend, entries);
    } else if (name.endsWith("DbContext.cs") && directory.split(sep).at(-1) === "Persistence") {
      const match = contextDeclaration.exec(readFileSync(path, "utf8"));
      if (match?.[1]) {
        entries.push({ key: contextKey(match[1]), contextName: match[1], project: owningProject(directory, backend) });
      }
    }
  }
}

function owningProject(directory: string, backend: string): string {
  for (let current = directory; current.startsWith(backend) && current !== backend; current = dirname(current)) {
    if (readdirSync(current).some((name) => name.endsWith(".csproj"))) {
      return relative(backend, current).split(sep).join(posix.sep);
    }
  }

  throw new Error(`no project file owns ${relative(backend, directory)}.`);
}
