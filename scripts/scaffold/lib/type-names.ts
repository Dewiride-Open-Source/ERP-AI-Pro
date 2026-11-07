import { existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, join, posix, relative, sep } from "node:path";
import { fileURLToPath } from "node:url";

import { isPascalName, type ScaffoldInput } from "./names.ts";

export type EntityNamespaces = { readonly external: readonly string[]; readonly repository: readonly string[] };

type TemplateFile = { readonly path: string; readonly text: string };

export const implicitNamespaces = ["System", "System.Collections.Generic", "System.IO", "System.Linq", "System.Net.Http", "System.Threading", "System.Threading.Tasks"] as const;

export const testNamespaces = ["Xunit"] as const;

export const dotnetTypeNamesFile = join(dirname(fileURLToPath(import.meta.url)), "dotnet-type-names.json");

const entityReference = /(?<![A-Za-z0-9_.])__Entity__(?![A-Za-z0-9_])/;
const usingDirective = /^using ([A-Za-z_][A-Za-z0-9_.]*);/gm;
const fileScopedNamespace = /^namespace ([A-Za-z_][A-Za-z0-9_.]*);/m;
const publicTopLevelType = /^public (?:(?:abstract|partial|readonly|ref|sealed|static|unsafe) )*(?:record (?:class|struct)|class|enum|interface|record|struct) (?!(?:class|struct)\b)([A-Za-z_][A-Za-z0-9_]*)(?![A-Za-z0-9_<])/gm;
const memberDeclaration = /^ {4}(?:public|private|protected|internal)\b[^=(;{\n]*?\b([A-Z][A-Za-z0-9_]*)\s*(?:[({=;]|$)/gm;
const repositoryFolders = ["backend/BuildingBlocks", "backend/Tests/Shared"] as const;
const skippedFolders = new Set(["bin", "obj", "artifacts", "node_modules"]);

// The namespaces imported, implicitly or by a using directive, by every generated C# file that names the entity on its own:
// a type of the same name in any of them makes the entity's name ambiguous there (CS0104).
export function entityNamespacesOf(templateRoot: string): EntityNamespaces {
  const external = new Set<string>();
  const repository = new Set<string>();
  for (const template of backendTemplates(templateRoot)) {
    if (!entityReference.test(template.text)) continue;
    for (const namespace of [...implicitNamespaces, ...(template.path.includes("/Tests/") ? testNamespaces : [])]) external.add(namespace);
    for (const [, namespace] of template.text.matchAll(usingDirective)) {
      if ((namespace as string).includes("__")) continue;
      ((namespace as string).startsWith("Dewiride.") ? repository : external).add(namespace as string);
    }
  }

  return { external: [...external].sort(), repository: [...repository].sort() };
}

export function typeNamespacesOf(templateRoot: string): string[] {
  return [...new Set([...implicitNamespaces, ...testNamespaces, ...entityNamespacesOf(templateRoot).external])].sort();
}

export function dotnetTypeNames(): ReadonlyMap<string, ReadonlySet<string>> {
  const document = JSON.parse(readFileSync(dotnetTypeNamesFile, "utf8")) as Record<string, readonly string[]>;
  return new Map(Object.entries(document).map(([namespace, names]) => [namespace, new Set(names)]));
}

export function repositoryTypeNames(root: string, namespaces: readonly string[]): ReadonlyMap<string, ReadonlySet<string>> {
  const wanted = new Set(namespaces);
  const types = new Map<string, Set<string>>();
  for (const folder of repositoryFolders) {
    for (const file of filesUnder(join(root, folder), (name) => name.endsWith(".cs"))) {
      const text = readFileSync(file, "utf8");
      const namespace = fileScopedNamespace.exec(text)?.[1];
      if (namespace === undefined || !wanted.has(namespace)) continue;
      const names = types.get(namespace) ?? new Set<string>();
      for (const [, name] of text.matchAll(publicTopLevelType)) names.add(name as string);
      types.set(namespace, names);
    }
  }

  return types;
}

export function entityMemberNamesOf(templateRoot: string): string[] {
  const entity = backendTemplates(templateRoot).find((template) => posix.basename(template.path) === "__Entity__.cs.tmpl");
  return entity === undefined ? [] : [...new Set([...entity.text.matchAll(memberDeclaration)].map((match) => match[1] as string))].sort();
}

export function typeNameProblems(root: string, input: ScaffoldInput, templateRoot: string): string[] {
  const problems: string[] = [];
  const dotnet = dotnetTypeNames();
  const { external, repository } = entityNamespacesOf(templateRoot);
  const declared = repositoryTypeNames(root, repository);
  const holderOf = (name: string, namespaces: readonly string[], types: ReadonlyMap<string, ReadonlySet<string>>): string | undefined =>
    namespaces.find((namespace) => types.get(namespace)?.has(name));

  if (isPascalName(input.entity)) {
    const holder = holderOf(input.entity, external, dotnet) ?? holderOf(input.entity, repository, declared);
    if (holder !== undefined) problems.push(`the entity "${input.entity}" is also the type ${holder}.${input.entity}, which the generated files that use the entity import, so the name would be ambiguous there`);
    if (entityMemberNamesOf(templateRoot).includes(input.entity)) problems.push(`the entity "${input.entity}" would be a class declaring a member of its own name, which C# refuses`);
  }

  const parents = { domain: "Dewiride.Erp.Modules", module: `Dewiride.Erp.Modules.${input.domain}`, entities: `Dewiride.Erp.Modules.${input.domain}.${input.module}` };
  for (const [role, name] of Object.entries({ domain: input.domain, module: input.module, entities: input.entities })) {
    if (!isPascalName(name)) continue;
    const holder = holderOf(name, [...implicitNamespaces, ...testNamespaces], dotnet);
    const parent = parents[role as keyof typeof parents];
    if (holder !== undefined) problems.push(`the ${role} "${name}" makes the namespace ${parent}.${name}, which hides the type ${holder}.${name} from the code in ${parent} that names it on its own`);
  }

  return problems;
}

function backendTemplates(templateRoot: string): TemplateFile[] {
  return filesUnder(join(templateRoot, "backend"), (name) => name.endsWith(".cs.tmpl")).map((file) => ({
    path: relative(templateRoot, file).split(sep).join(posix.sep),
    text: readFileSync(file, "utf8"),
  }));
}

function filesUnder(start: string, accept: (name: string) => boolean): string[] {
  if (!existsSync(start)) return [];
  const files: string[] = [];
  for (const entry of readdirSync(start, { withFileTypes: true })) {
    const path = join(start, entry.name);
    if (entry.isDirectory() && !skippedFolders.has(entry.name)) files.push(...filesUnder(path, accept));
    else if (entry.isFile() && accept(entry.name)) files.push(path);
  }

  return files.sort();
}
