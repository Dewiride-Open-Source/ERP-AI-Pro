import { existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, join, posix, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

export const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
export const backendRoot = join(repoRoot, "backend");

export type DbContextEntry = {
  key: string;
  contextName: string;
  fullName: string;
  project: string;
};

const searchRoots = ["Modules", "BuildingBlocks"];
const skippedDirectories = new Set(["bin", "obj", "artifacts", "Tests", "node_modules"]);
const contextDeclaration = /\bclass\s+(\w+DbContext)\b[^{]*:\s*ModuleDbContext\b/;
const namespaceDeclaration = /^[ \t]*namespace[ \t]+([\w.]+)\s*[;{]/m;

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

export function contextKey(location: string, contextName: string): string {
  const [root, domain, module] = location.split(posix.sep);
  if (root === "Modules" && domain && module) return `${kebabCase(domain)}-${kebabCase(module)}`;
  return kebabCase(contextName.replace(/DbContext$/, ""));
}

function kebabCase(name: string): string {
  return name.replace(/([a-z0-9])([A-Z])/g, "$1-$2").toLowerCase();
}

function collect(directory: string, backend: string, entries: DbContextEntry[]): void {
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    if (skippedDirectories.has(entry.name)) continue;
    const path = join(directory, entry.name);
    if (entry.isDirectory()) {
      collect(path, backend, entries);
    } else if (entry.isFile() && entry.name.endsWith("DbContext.cs") && directory.split(sep).at(-1) === "Persistence") {
      const source = readFileSync(path, "utf8");
      const contextName = contextDeclaration.exec(source)?.[1];
      if (contextName) {
        const location = relative(backend, directory).split(sep).join(posix.sep);
        const namespace = namespaceOf(source, posix.join(location, entry.name));
        entries.push({
          key: contextKey(location, contextName),
          contextName,
          fullName: `${namespace}.${contextName}`,
          project: owningProject(directory, backend),
        });
      }
    }
  }
}

function namespaceOf(source: string, file: string): string {
  const namespace = namespaceDeclaration.exec(source)?.[1];
  if (!namespace) throw new Error(`${file} declares no namespace, so dotnet ef cannot be given its context's full name.`);
  return namespace;
}

function owningProject(directory: string, backend: string): string {
  for (let current = directory; current.startsWith(backend) && current !== backend; current = dirname(current)) {
    if (readdirSync(current).some((name) => name.endsWith(".csproj"))) {
      return relative(backend, current).split(sep).join(posix.sep);
    }
  }

  throw new Error(`no project file owns ${relative(backend, directory)}.`);
}
