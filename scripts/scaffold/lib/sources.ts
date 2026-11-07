import { existsSync, readdirSync, readFileSync } from "node:fs";
import { join, posix, relative, sep } from "node:path";

import { paths } from "./registrations/paths.ts";
import { areaOf, type NavigationArea, type NavigationManifest, navigationManifestsOf } from "./registrations/web.ts";

export type SourceFile = { readonly path: string; readonly text: string };

export type PermissionSource = { readonly name: string; readonly employees: boolean; readonly description: string; readonly file: string };

export type ChangeKeySource = { readonly key: string | undefined; readonly argument: string; readonly file: string };

const skippedFolders = new Set(["bin", "obj", "artifacts", "node_modules", "Tests", ".git", ".next"]);
const quotedText = String.raw`"(?:[^"\\]|\\.)*"`;
const classOrConstant = /\bclass\s+(\w+)\b|\bconst\s+string\s+(\w+)\s*=\s*("(?:[^"\\]|\\.)*")/g;
const permissionDeclaration = new RegExp(String.raw`\bnew\(\s*(\w+)\.(\w+)\s*,\s*\[([^\]]*)\]\s*,\s*(${quotedText})\s*\)`, "g");
const schemaConstant = new RegExp(String.raw`\bconst\s+string\s+Schema(?:Name)?\s*=\s*(${quotedText})|\bEnsureSchema\(\s*name:\s*(${quotedText})`, "g");
const routePrefix = new RegExp(String.raw`\bRoutePrefix:\s*(${quotedText})`, "g");
const featureFlagConstant = new RegExp(String.raw`\bFeatureFlag\s*=\s*(${quotedText})`, "g");
const changeKeyCall = /\.RecordChangesAs\(([^)]*)\)/g;
const homeTitle = new RegExp(String.raw`\bexport const homeTitle\s*=\s*(${quotedText})`);

export function productSources(root: string): SourceFile[] {
  return [paths.backendModules, paths.backendBuildingBlocks].flatMap((folder) => filesUnder(root, folder, (name) => name.endsWith(".cs")));
}

export function filesUnder(root: string, folder: string, accept: (name: string) => boolean): SourceFile[] {
  const start = join(root, folder);
  if (!existsSync(start)) return [];
  const files: SourceFile[] = [];
  const visit = (directory: string): void => {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      const path = join(directory, entry.name);
      if (entry.isDirectory() && !skippedFolders.has(entry.name)) visit(path);
      else if (entry.isFile() && accept(entry.name)) files.push({ path: relative(root, path).split(sep).join(posix.sep), text: readFileSync(path, "utf8") });
    }
  };
  visit(start);
  return files.sort((left, right) => (left.path < right.path ? -1 : 1));
}

export function folderNames(root: string, folder: string): string[] {
  const directory = join(root, folder);
  if (!existsSync(directory)) return [];
  return readdirSync(directory, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name);
}

export function schemasOf(sources: readonly SourceFile[]): { readonly schema: string; readonly file: string }[] {
  return sources.flatMap(({ path, text }) => [...text.matchAll(schemaConstant)].map((match) => ({ schema: JSON.parse((match[1] ?? match[2]) as string) as string, file: path })));
}

export function routePrefixesOf(sources: readonly SourceFile[]): { readonly prefix: string; readonly file: string }[] {
  return sources.flatMap(({ path, text }) => [...text.matchAll(routePrefix)].map((match) => ({ prefix: JSON.parse(match[1] as string) as string, file: path })));
}

export function featureFlagsOf(sources: readonly SourceFile[]): { readonly flag: string; readonly file: string }[] {
  return sources.flatMap(({ path, text }) => [...text.matchAll(featureFlagConstant)].map((match) => ({ flag: JSON.parse(match[1] as string) as string, file: path })));
}

// A descriptor names each permission by a constant of its module's Permissions class in Contracts, so the constants are read
// first and the declarations resolve through them.
export function permissionsOf(sources: readonly SourceFile[]): PermissionSource[] {
  const constants = new Map<string, string>();
  for (const { text } of sources) {
    let owner = "";
    for (const match of text.matchAll(classOrConstant)) {
      if (match[1] !== undefined) owner = match[1];
      else if (owner.endsWith("Permissions")) constants.set(`${owner}.${match[2]}`, JSON.parse(match[3] as string) as string);
    }
  }

  return sources.flatMap(({ path, text }) =>
    [...text.matchAll(permissionDeclaration)].map((match) => ({
      name: constants.get(`${match[1]}.${match[2]}`) ?? `${match[1]}.${match[2]}`,
      employees: /\bErpRole\.Employee\b/.test(match[3] as string),
      description: JSON.parse(match[4] as string) as string,
      file: path,
    })),
  );
}

export function recordedChangeKeysOf(sources: readonly SourceFile[]): ChangeKeySource[] {
  return sources.flatMap(({ path, text }) =>
    [...text.matchAll(changeKeyCall)].map((match) => {
      const argument = (match[1] as string).trim();
      return { key: /^"[^"\\]*"$/.test(argument) ? (JSON.parse(argument) as string) : undefined, argument, file: path };
    }),
  );
}

export function navigationOf(root: string): { readonly manifests: (NavigationManifest & { readonly file: string })[]; readonly areas: (NavigationArea & { readonly file: string })[] } {
  const manifests = filesUnder(root, paths.features, (name) => name === "nav.ts").flatMap(({ path, text }) => navigationManifestsOf(text).map((manifest) => ({ ...manifest, file: path })));
  const areas = filesUnder(root, paths.features, (name) => name.endsWith("-area.ts")).flatMap(({ path, text }) => {
    const area = path.split("/").at(-2) === "_shared" ? areaOf(text) : undefined;
    return area === undefined ? [] : [{ ...area, file: path }];
  });
  return { manifests, areas };
}

export function shellTitlesOf(root: string): string[] {
  const file = join(root, paths.navigationTrail);
  if (!existsSync(file)) return [];
  const literal = homeTitle.exec(readFileSync(file, "utf8"))?.[1];
  return literal === undefined ? [] : [JSON.parse(literal) as string];
}

export function iconExportsOf(root: string): ReadonlySet<string> | undefined {
  const manifest = join(root, paths.lucide, "package.json");
  if (!existsSync(manifest)) return undefined;
  const { types, typings } = JSON.parse(readFileSync(manifest, "utf8")) as { types?: string; typings?: string };
  const declarations = join(root, paths.lucide, types ?? typings ?? "dist/lucide-react.d.ts");
  if (!existsSync(declarations)) return undefined;
  const names = new Set<string>();
  for (const block of readFileSync(declarations, "utf8").matchAll(/^export \{([^}]*)\};/gm)) {
    for (const part of (block[1] as string).split(",")) {
      const name = part.trim().split(" as ").at(-1)?.trim();
      if (name) names.add(name);
    }
  }

  return names;
}
