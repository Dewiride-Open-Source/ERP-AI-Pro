export type YamlValue = string | YamlValue[] | { [key: string]: YamlValue };

type YamlMap = { [key: string]: YamlValue };

function unquote(text: string): string {
  const trimmed = text.trim();
  if (trimmed.startsWith("'") && trimmed.endsWith("'") && trimmed.length >= 2) return trimmed.slice(1, -1).replaceAll("''", "'");
  if (trimmed.startsWith('"') && trimmed.endsWith('"') && trimmed.length >= 2) return JSON.parse(trimmed) as string;
  return trimmed;
}

function splitFlow(inner: string): string[] {
  const parts: string[] = [];
  let depth = 0;
  let quote: string | undefined;
  let current = "";
  for (const char of inner) {
    if (quote !== undefined) {
      if (char === quote) quote = undefined;
    } else if (char === "'" || char === '"') quote = char;
    else if (char === "{" || char === "[") depth += 1;
    else if (char === "}" || char === "]") depth -= 1;
    else if (char === "," && depth === 0) {
      parts.push(current);
      current = "";
      continue;
    }
    current += char;
  }
  if (current.trim() !== "") parts.push(current);
  return parts.map((part) => part.trim());
}

function keyAndRest(text: string): [string, string] | undefined {
  let index = 0;
  if (text.startsWith("'") || text.startsWith('"')) {
    const quote = text[0] as string;
    index = 1;
    while (index < text.length) {
      if (text[index] === quote) {
        if (quote === "'" && text[index + 1] === "'") index += 2;
        else break;
      } else index += 1;
    }
    index += 1;
    if (text[index] !== ":") return undefined;
  } else {
    index = text.search(/:(?:\s|$)/);
    if (index <= 0) return undefined;
  }
  return [unquote(text.slice(0, index)), text.slice(index + 1).trim()];
}

function scalar(text: string): YamlValue {
  if (text.startsWith("[") && text.endsWith("]")) return splitFlow(text.slice(1, -1)).map((item) => scalar(item));
  if (text.startsWith("{") && text.endsWith("}")) {
    const map: YamlMap = {};
    for (const part of splitFlow(text.slice(1, -1))) {
      const pair = keyAndRest(part);
      if (pair === undefined) throw new Error(`the lockfile holds a flow mapping entry without a key: ${part}`);
      map[pair[0]] = scalar(pair[1]);
    }
    return map;
  }
  return unquote(text);
}

export function parseLockfileDocuments(text: string): YamlMap[] {
  const documents: YamlMap[] = [];
  let root: YamlMap = {};
  let stack: { indent: number; value: YamlMap | YamlValue[] }[] = [{ indent: -1, value: root }];
  let pendingKey: { indent: number; owner: YamlMap; key: string } | undefined;
  const lines = text.replace(/\r\n?/g, "\n").split("\n");
  for (const line of lines) {
    if (line.trim() === "" || line.trimStart().startsWith("#")) continue;
    if (line === "---") {
      if (Object.keys(root).length > 0) documents.push(root);
      root = {};
      stack = [{ indent: -1, value: root }];
      pendingKey = undefined;
      continue;
    }
    const indent = line.length - line.trimStart().length;
    const content = line.trim();
    if (pendingKey !== undefined) {
      const container: YamlMap | YamlValue[] = content.startsWith("- ") ? [] : {};
      if (indent > pendingKey.indent) {
        pendingKey.owner[pendingKey.key] = container;
        stack.push({ indent: pendingKey.indent, value: container });
      } else pendingKey.owner[pendingKey.key] = {};
      pendingKey = undefined;
    }
    while (stack.length > 1 && indent <= (stack.at(-1)?.indent ?? -1)) stack.pop();
    const parent = stack.at(-1)?.value as YamlMap | YamlValue[];
    if (content.startsWith("- ")) {
      if (!Array.isArray(parent)) throw new Error(`the lockfile holds a sequence item outside a sequence: ${content}`);
      parent.push(scalar(content.slice(2)));
      continue;
    }
    if (Array.isArray(parent)) throw new Error(`the lockfile holds a mapping entry inside a sequence: ${content}`);
    const pair = keyAndRest(content);
    if (pair === undefined) throw new Error(`the lockfile holds a line that is not a mapping entry: ${content}`);
    const [key, rest] = pair;
    if (rest === "") pendingKey = { indent, owner: parent, key };
    else parent[key] = scalar(rest);
  }
  if (pendingKey !== undefined) pendingKey.owner[pendingKey.key] = {};
  if (Object.keys(root).length > 0) documents.push(root);
  return documents;
}

export type LockedPackage = { readonly name: string; readonly version: string; readonly key: string };

export type Lockfile = {
  readonly importers: Readonly<Record<string, YamlMap>>;
  readonly packages: Readonly<Record<string, YamlMap>>;
  readonly snapshots: Readonly<Record<string, YamlMap>>;
};

const asMap = (value: YamlValue | undefined): YamlMap => (value !== undefined && typeof value === "object" && !Array.isArray(value) ? value : {});

const asList = (value: YamlValue | undefined): string[] => (Array.isArray(value) ? value.filter((item): item is string => typeof item === "string") : []);

export function readLockfile(text: string, importer: string): Lockfile {
  const document = parseLockfileDocuments(text).find((candidate) => asMap(candidate.importers)[importer] !== undefined);
  if (document === undefined) throw new Error(`pnpm-lock.yaml has no importer ${importer}`);
  const records = (value: YamlValue | undefined) => Object.fromEntries(Object.entries(asMap(value)).map(([key, entry]) => [key, asMap(entry)]));
  return { importers: records(document.importers), packages: records(document.packages), snapshots: records(document.snapshots) };
}

export function splitPackageKey(key: string): { name: string; version: string } {
  const base = key.includes("(") ? key.slice(0, key.indexOf("(")) : key;
  const at = base.lastIndexOf("@");
  if (at <= 0) throw new Error(`the lockfile key ${key} is not <name>@<version>`);
  return { name: base.slice(0, at), version: base.slice(at + 1) };
}

function snapshotKey(name: string, reference: string): string {
  const head = reference.includes("(") ? reference.slice(0, reference.indexOf("(")) : reference;
  return head.lastIndexOf("@") > 0 ? reference : `${name}@${reference}`;
}

function installable(list: readonly string[], platform: string): boolean {
  const allowed = list.filter((entry) => !entry.startsWith("!"));
  if (allowed.length > 0) return allowed.includes(platform);
  return !list.includes(`!${platform}`);
}

export function isPlatformSpecific(entry: YamlMap): boolean {
  if (entry.cpu !== undefined || entry.libc !== undefined) return true;
  const os = asList(entry.os);
  return os.length > 0 && !(installable(os, "linux") && installable(os, "win32"));
}

export type ClosureOptions = { readonly excluded: (name: string) => boolean };

export function productionClosure(lockfile: Lockfile, importer: string, options: ClosureOptions): LockedPackage[] {
  const found = new Map<string, LockedPackage>();
  const visitedImporters = new Set<string>();
  const visitedSnapshots = new Set<string>();

  const visitImporter = (path: string) => {
    if (visitedImporters.has(path)) return;
    visitedImporters.add(path);
    const entry = lockfile.importers[path];
    if (entry === undefined) throw new Error(`pnpm-lock.yaml has no importer ${path}`);
    for (const group of ["dependencies", "optionalDependencies"]) {
      for (const [name, dependency] of Object.entries(asMap(entry[group]))) {
        const version = asMap(dependency).version;
        if (typeof version !== "string") throw new Error(`the importer ${path} names ${name} without a version`);
        if (version.startsWith("link:")) visitImporter(normaliseImporter(path, version.slice("link:".length)));
        else visitSnapshot(snapshotKey(name, version));
      }
    }
  };

  const visitSnapshot = (key: string) => {
    if (visitedSnapshots.has(key)) return;
    visitedSnapshots.add(key);
    const { name, version } = splitPackageKey(key);
    const base = `${name}@${version}`;
    const metadata = lockfile.packages[base];
    if (metadata === undefined) throw new Error(`pnpm-lock.yaml has no package entry ${base}`);
    if (options.excluded(name) || isPlatformSpecific(metadata)) return;
    found.set(base, { name, version, key: base });
    const snapshot = lockfile.snapshots[key] ?? {};
    const peers = new Set(Object.keys(asMap(metadata.peerDependencies)));
    for (const [dependency, reference] of Object.entries(asMap(snapshot.dependencies))) {
      if (typeof reference === "string") visitSnapshot(snapshotKey(dependency, reference));
    }
    for (const [dependency, reference] of Object.entries(asMap(snapshot.optionalDependencies))) {
      if (typeof reference === "string" && !peers.has(dependency)) visitSnapshot(snapshotKey(dependency, reference));
    }
  };

  visitImporter(importer);
  return [...found.values()].sort((a, b) => (a.key < b.key ? -1 : Number(a.key > b.key)));
}

function normaliseImporter(from: string, relative: string): string {
  const segments = from === "." ? [] : from.split("/");
  for (const segment of relative.split("/")) {
    if (segment === "..") segments.pop();
    else if (segment !== "." && segment !== "") segments.push(segment);
  }
  return segments.length === 0 ? "." : segments.join("/");
}
