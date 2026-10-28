import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { join } from "node:path";

import type { ImageFiles } from "./container-files.ts";
import { byCodeUnits, type Component, genericPurl, noAssertion, npmPurl, uniqueLines } from "./inventory.ts";
import { canonicalText, identifyLicence, licenceIds, spdxExpression, splitCopyright, type TextTable, textBase } from "./licence-texts.ts";
import { productionClosure, readLockfile } from "./pnpm-lock.ts";

type PackageManifest = { name?: string; version?: string; license?: unknown; licenses?: unknown; author?: unknown };

const licenceFile = /^(?!.*(?:3rd|third)-?party)(?:licen[cs]e|copying)(?:[.-].*)?$/i;
const noticeFile = /^(?:notice(?:\..*)?|(?:licen[cs]e|notice)[.-]?(?:3rd|third)-?party.*)$/i;
const shortestLicenceText = 100;

export const excludedFromWebImage = (name: string): boolean => name === "sharp" || name.startsWith("@img/");

export function declaredLicence(manifest: PackageManifest): string | undefined {
  if (typeof manifest.license === "string") return manifest.license;
  if (typeof manifest.license === "object" && manifest.license !== null && typeof (manifest.license as { type?: unknown }).type === "string") return (manifest.license as { type: string }).type;
  if (Array.isArray(manifest.licenses)) {
    const types = manifest.licenses.map((entry) => (typeof entry === "string" ? entry : (entry as { type?: unknown }).type)).filter((entry): entry is string => typeof entry === "string");
    if (types.length > 0) return types.length === 1 ? types[0] : `(${types.join(" OR ")})`;
  }
  return undefined;
}

function personOf(author: unknown): string | undefined {
  if (typeof author === "string") {
    const parsed = /^([^<(]+?)\s*(?:<([^>]+)>)?\s*(?:\(([^)]+)\))?$/.exec(author.trim());
    if (parsed === null || parsed[1] === undefined) return undefined;
    return parsed[2] === undefined ? `Person: ${parsed[1]}` : `Person: ${parsed[1]} (${parsed[2]})`;
  }
  if (typeof author === "object" && author !== null && typeof (author as { name?: unknown }).name === "string") {
    const { name, email } = author as { name: string; email?: unknown };
    return typeof email === "string" ? `Person: ${name} (${email})` : `Person: ${name}`;
  }
  return undefined;
}

export function pnpmFolders(storeRoot: string): Map<string, string> {
  return new Map(readdirSync(storeRoot).map((entry) => [entry, join(storeRoot, entry)]));
}

function namesPackage(entry: string, escaped: string): boolean {
  if (entry === escaped || entry.startsWith(`${escaped}_`)) return true;
  const hashed = /^(.+)_[0-9a-f]{32}$/.exec(entry);
  return hashed !== null && escaped.startsWith(hashed[1] as string);
}

export function packageFolder(folders: ReadonlyMap<string, string>, name: string, version: string): string {
  const escaped = `${name.replace("/", "+")}@${version}`;
  for (const [entry, path] of folders) {
    if (!namesPackage(entry, escaped)) continue;
    const folder = join(path, "node_modules", ...name.split("/"));
    const manifestPath = join(folder, "package.json");
    if (!existsSync(manifestPath)) continue;
    const manifest = JSON.parse(readFileSync(manifestPath, "utf8")) as PackageManifest;
    if (manifest.name === name && manifest.version === version) return folder;
  }
  throw new Error(`${name} ${version} is not installed under frontend/node_modules/.pnpm; run "pnpm install --frozen-lockfile" in frontend first`);
}

function filesMatching(folder: string, pattern: RegExp): string[] {
  return readdirSync(folder, { withFileTypes: true })
    .filter((entry) => entry.isFile() && pattern.test(entry.name))
    .map((entry) => entry.name)
    .sort();
}

function bundledLicences(folder: string, owner: string): { title: string; text: string }[] {
  const found: { title: string; text: string }[] = [];
  const visit = (directory: string, depth: number) => {
    for (const entry of readdirSync(directory, { withFileTypes: true }).sort((a, b) => byCodeUnits(a.name, b.name))) {
      if (!entry.isDirectory() || entry.name === "node_modules") continue;
      const child = join(directory, entry.name);
      const manifestPath = join(child, "package.json");
      if (existsSync(manifestPath)) {
        const manifest = JSON.parse(readFileSync(manifestPath, "utf8")) as PackageManifest;
        const licences = filesMatching(child, licenceFile);
        if (typeof manifest.name === "string" && licences.length > 0) {
          const title = `${manifest.name}${typeof manifest.version === "string" ? ` ${manifest.version}` : ""}, bundled in ${owner}`;
          for (const file of licences) found.push({ title, text: readFileSync(join(child, file), "utf8").replace(/\r\n?/g, "\n").trim() });
        }
      }
      if (depth < 6) visit(child, depth + 1);
    }
  };
  visit(folder, 0);
  return found;
}

export function npmComponent(folder: string, table: TextTable): Component {
  const manifest = JSON.parse(readFileSync(join(folder, "package.json"), "utf8")) as PackageManifest;
  if (typeof manifest.name !== "string" || typeof manifest.version !== "string") throw new Error(`${folder}/package.json names no name or version`);
  const declared = declaredLicence(manifest);
  const expression = declared === undefined ? undefined : spdxExpression(declared);
  const files = filesMatching(folder, licenceFile);
  const copyright: string[] = [];
  const texts: string[] = [];
  for (const file of files) {
    const split = splitCopyright(readFileSync(join(folder, file), "utf8"));
    copyright.push(...split.copyright);
    if (split.body.length >= shortestLicenceText) texts.push(table.add(expression ?? declared ?? identifyLicence(split.body) ?? `the licence of ${manifest.name}`, split.body));
  }
  if (texts.length === 0 && expression !== undefined) for (const id of licenceIds(expression)) texts.push(table.add(textBase(id), canonicalText(textBase(id))));
  const notices = [
    ...filesMatching(folder, noticeFile).map((file) => table.add(`NOTICE of ${manifest.name} ${manifest.version}`, readFileSync(join(folder, file), "utf8").replace(/\r\n?/g, "\n").trim())),
    ...bundledLicences(folder, manifest.name).map(({ title, text }) => table.add(title, text)),
  ];
  const bareName = manifest.name.includes("/") ? (manifest.name.split("/")[1] as string) : manifest.name;
  return {
    purl: npmPurl(manifest.name, manifest.version),
    kind: "npm",
    name: manifest.name,
    version: manifest.version,
    licence: expression ?? declared ?? noAssertion,
    copyright: uniqueLines(copyright),
    texts: [...new Set(texts)],
    notices: [...new Set(notices)],
    inImage: [],
    source: `https://registry.npmjs.org/${manifest.name}/-/${bareName}-${manifest.version}.tgz`,
    supplier: personOf(manifest.author),
  };
}

export function webPackageComponents(frontendRoot: string, table: TextTable): Component[] {
  const lockfile = readLockfile(readFileSync(join(frontendRoot, "pnpm-lock.yaml"), "utf8"), "apps/web");
  const storeRoot = join(frontendRoot, "node_modules", ".pnpm");
  if (!existsSync(storeRoot) || !statSync(storeRoot).isDirectory()) throw new Error(`${storeRoot} is missing; run "pnpm install --frozen-lockfile" in frontend first`);
  const folders = pnpmFolders(storeRoot);
  return productionClosure(lockfile, "apps/web", { excluded: excludedFromWebImage }).map(({ name, version }) => {
    const component = npmComponent(packageFolder(folders, name, version), table);
    if (component.name !== name || component.version !== version) throw new Error(`${name} ${version} is installed as ${component.name} ${component.version}`);
    return component;
  });
}

function manifestIn(files: ImageFiles, path: string): PackageManifest {
  const text = files.text(path);
  if (text === undefined) throw new Error(`the base image has no /${path}`);
  return JSON.parse(text) as PackageManifest;
}

function copyrightBefore(text: string, marker: RegExp): string[] {
  const lines = text.replace(/\r\n?/g, "\n").split("\n");
  const end = lines.findIndex((line) => marker.test(line));
  return uniqueLines(splitCopyright(lines.slice(0, end === -1 ? lines.length : end).join("\n")).copyright);
}

export function nodeRuntimeComponents(files: ImageFiles, env: Readonly<Record<string, string>>): Component[] {
  const nodeVersion = env.NODE_VERSION;
  const yarnVersion = env.YARN_VERSION;
  if (nodeVersion === undefined) throw new Error("the Node.js base image sets no NODE_VERSION");
  const nodeLicence = files.text("usr/local/LICENSE");
  if (nodeLicence === undefined) throw new Error("the Node.js base image has no /usr/local/LICENSE");
  const tool = (path: string, name: string, licencePath: string, repository: string): Component => {
    const manifest = manifestIn(files, `${path}/package.json`);
    const version = manifest.version ?? "";
    const licence = files.text(licencePath);
    return {
      purl: npmPurl(name, version),
      kind: "runtime",
      name,
      version,
      licence: spdxExpression(declaredLicence(manifest) ?? "") ?? noAssertion,
      copyright: licence === undefined ? [] : uniqueLines(splitCopyright(licence).copyright),
      texts: [],
      notices: [],
      inImage: [`/${licencePath}`, ...(files.list(`${path}/node_modules`).length > 0 ? [`/${path}/node_modules`] : [])],
      source: `https://github.com/${repository}/tree/v${version}`,
      detail: `part of the Node.js base image; ${licence === undefined ? "its licence is named in its package.json" : `its licence is /${licencePath}`}${files.list(`${path}/node_modules`).length > 0 ? `, and the packages it bundles keep their licence files in /${path}/node_modules` : ""}`,
    };
  };
  const yarnFolder = files.list("opt").find((entry) => /^opt\/yarn-v/.test(entry.path))?.path;
  return [
    {
      purl: genericPurl("node", nodeVersion, { download_url: `https://nodejs.org/dist/v${nodeVersion}/node-v${nodeVersion}-linux-x64.tar.xz` }),
      kind: "runtime",
      name: "Node.js",
      version: nodeVersion,
      licence: "MIT",
      copyright: copyrightBefore(nodeLicence, /^The externally maintained libraries used by Node\.js are:/),
      texts: [],
      notices: [],
      inImage: ["/usr/local/LICENSE"],
      source: `https://github.com/nodejs/node/tree/v${nodeVersion}`,
      detail: "the runtime of the Node.js base image; /usr/local/LICENSE holds its licence and those of the libraries it bundles",
    },
    tool("usr/local/lib/node_modules/npm", "npm", "usr/local/lib/node_modules/npm/LICENSE", "npm/cli"),
    tool("usr/local/lib/node_modules/corepack", "corepack", "usr/local/lib/node_modules/corepack/LICENSE.md", "nodejs/corepack"),
    ...(yarnFolder === undefined || yarnVersion === undefined ? [] : [tool(yarnFolder, "yarn", `${yarnFolder}/LICENSE`, "yarnpkg/yarn")]),
  ];
}
