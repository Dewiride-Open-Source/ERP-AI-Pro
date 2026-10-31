import { createHash } from "node:crypto";
import { posix } from "node:path";
import { Readable } from "node:stream";
import { createGunzip } from "node:zlib";

import { collect, readTar } from "../../checks/lib/image-contents.ts";
import { byCodeUnits, type Component, golangPurl, noAssertion, uniqueLines } from "./inventory.ts";
import { canonicalText, identifyLicence, splitCopyright, type TextTable } from "./licence-texts.ts";

export type GoModule = { readonly path: string; readonly version: string; readonly sum: string };

export type GoBuildInfo = { readonly goVersion: string; readonly main: GoModule; readonly dependencies: readonly GoModule[]; readonly settings: Readonly<Record<string, string>> };

const buildInfoMagic = Buffer.from("\xff Go buildinf:", "latin1");
const buildInfoHeaderSize = 32;
const inlineStrings = 0x2;

function uvarint(data: Uint8Array, offset: number): { value: number; next: number } {
  let value = 0;
  let shift = 0;
  for (let index = offset; index < Math.min(data.length, offset + 10); index += 1) {
    const byte = data[index] as number;
    value += (byte & 0x7f) * 2 ** shift;
    if ((byte & 0x80) === 0) return { value, next: index + 1 };
    shift += 7;
  }
  throw new Error("the Go build information holds a malformed length");
}

function inlineString(data: Uint8Array, offset: number): { text: string; next: number } {
  const { value, next } = uvarint(data, offset);
  if (next + value > data.length) throw new Error("the Go build information is cut short");
  return { text: Buffer.from(data.subarray(next, next + value)).toString("utf8"), next: next + value };
}

export function goBuildInfo(binary: Uint8Array): GoBuildInfo {
  const bytes = Buffer.from(binary.buffer, binary.byteOffset, binary.byteLength);
  for (let offset = bytes.indexOf(buildInfoMagic); offset !== -1; offset = bytes.indexOf(buildInfoMagic, offset + 1)) {
    if (offset % 16 !== 0 || ((bytes[offset + 15] ?? 0) & inlineStrings) === 0) continue;
    const version = inlineString(bytes, offset + buildInfoHeaderSize);
    const framed = inlineString(bytes, version.next).text;
    if (framed.length < 33 || framed[framed.length - 17] !== "\n") throw new Error("the Go build information holds no module information");
    return parseModuleInfo(version.text, framed.slice(16, -16));
  }
  throw new Error("the binary holds no Go build information of Go 1.18 or later");
}

export function parseModuleInfo(goVersion: string, info: string): GoBuildInfo {
  let main: GoModule | undefined;
  const dependencies: GoModule[] = [];
  const settings: Record<string, string> = {};
  for (const line of info.split("\n")) {
    const [kind, ...fields] = line.split("\t");
    if (kind === "mod") main = { path: fields[0] ?? "", version: fields[1] ?? "", sum: fields[2] ?? "" };
    else if (kind === "dep") dependencies.push({ path: fields[0] ?? "", version: fields[1] ?? "", sum: fields[2] ?? "" });
    else if (kind === "=>") {
      const replaced = dependencies.pop();
      if (replaced === undefined) throw new Error("the Go build information replaces a module it does not name");
      dependencies.push({ path: fields[0] ?? "", version: fields[1] ?? "", sum: fields[2] ?? "" });
    } else if (kind === "build") {
      const setting = fields.join("\t");
      settings[setting.slice(0, setting.indexOf("="))] = setting.slice(setting.indexOf("=") + 1);
    }
  }
  if (main === undefined) throw new Error("the Go build information names no main module");
  return { goVersion, main, dependencies, settings };
}

export function moduleProxyPath(path: string): string {
  return path.replace(/[A-Z]/g, (letter) => `!${letter.toLowerCase()}`);
}

export const caddyArtifacts: Readonly<Record<string, string>> = {
  "2.11.7": "b430516910839fbaf35c0a9e9df80d1e2e30aa792530293c39f4a97a1b2c9060",
};

export const caddyArtifactUrl = (version: string): string => `https://github.com/caddyserver/caddy/releases/download/v${version}/caddy_${version}_buildable-artifact.tar.gz`;

export type SourceFile = { readonly path: string; readonly text: string };

const licenceFile = /^(?:licen[cs]e|copying|unlicense)(?:[.-].*)?$/i;
const noticeFile = /^(?:notice|patents)(?:\..*)?$/i;

export function isLegalFile(path: string): boolean {
  const name = posix.basename(path);
  return licenceFile.test(name) || noticeFile.test(name);
}

export async function caddySourceFiles(version: string): Promise<SourceFile[]> {
  const expected = caddyArtifacts[version];
  if (expected === undefined) {
    throw new Error(
      `the proxy's base image runs Caddy ${version}, whose buildable source artifact has no pinned SHA-256 in scripts/notices/lib/go.ts; read the digest GitHub lists for caddy_${version}_buildable-artifact.tar.gz on https://github.com/caddyserver/caddy/releases/tag/v${version} and add it to caddyArtifacts`,
    );
  }
  const response = await fetch(caddyArtifactUrl(version));
  if (!response.ok) throw new Error(`${caddyArtifactUrl(version)} answered ${response.status}`);
  const archive = new Uint8Array(await response.arrayBuffer());
  const actual = createHash("sha256").update(archive).digest("hex");
  if (actual !== expected) throw new Error(`${caddyArtifactUrl(version)} has the SHA-256 ${actual}, not the pinned ${expected}`);
  const files: SourceFile[] = [];
  for await (const { header, content } of readTar(Readable.from([archive]).pipe(createGunzip()))) {
    const path = posix.normalize(header.name).replace(/^\.\//, "");
    if ((header.type === "0" || header.type === "\0") && isLegalFile(path)) files.push({ path, text: new TextDecoder().decode(await collect(content)) });
  }
  return files.sort((a, b) => byCodeUnits(a.path, b.path));
}

export function owningModule(path: string, modules: readonly string[]): string | undefined {
  if (!path.startsWith("vendor/")) return undefined;
  const folder = posix.dirname(path.slice("vendor/".length));
  return modules.filter((module) => folder === module || folder.startsWith(`${module}/`)).sort((a, b) => b.length - a.length)[0];
}

function licenceOfTexts(bodies: readonly string[]): string {
  const ids = bodies.map(identifyLicence);
  if (ids.length === 0 || ids.some((id) => id === undefined)) return noAssertion;
  return [...new Set(ids as string[])].join(" AND ");
}

export function goModuleComponents(info: GoBuildInfo, sources: readonly SourceFile[], table: TextTable): Component[] {
  const paths = info.dependencies.map(({ path }) => path);
  return info.dependencies.map((module) => {
    const files = sources.filter((file) => owningModule(file.path, paths) === module.path);
    const licences = files.filter((file) => licenceFile.test(posix.basename(file.path)));
    if (licences.length === 0) throw new Error(`Caddy's source artifact holds no licence file for the Go module ${module.path} ${module.version}`);
    const split = licences.map((file) => ({ file, ...splitCopyright(file.text) }));
    const licence = licenceOfTexts(split.map(({ body }) => body));
    return {
      purl: golangPurl(module.path, module.version),
      kind: "golang",
      name: module.path,
      version: module.version,
      licence,
      copyright: uniqueLines(split.flatMap(({ copyright }) => copyright)),
      texts: [...new Set(split.filter(({ body }) => body !== "").map(({ file, body }) => table.add(identifyLicence(body) ?? `the licence in ${file.path.slice("vendor/".length)}`, body)))],
      notices: [...new Set(files.filter((file) => noticeFile.test(posix.basename(file.path))).map((file) => table.add(`${file.path.slice("vendor/".length)}`, file.text.replace(/\r\n?/g, "\n").trim())))],
      inImage: [],
      source: `https://proxy.golang.org/${moduleProxyPath(module.path)}/@v/${module.version}.zip`,
      detail: `built into /usr/bin/caddy; checksum ${module.sum}`,
    };
  });
}

export function goToolchainComponent(info: GoBuildInfo, table: TextTable): Component {
  const version = info.goVersion.replace(/^go/, "");
  const split = splitCopyright(canonicalText("Go"));
  return {
    purl: golangPurl("stdlib", version),
    kind: "golang",
    name: "Go standard library",
    version,
    licence: "BSD-3-Clause",
    copyright: split.copyright,
    texts: [table.add("BSD-3-Clause", split.body)],
    notices: [],
    inImage: [],
    source: `https://go.googlesource.com/go/+/refs/tags/${info.goVersion}`,
    detail: `the Go runtime and standard library that ${info.goVersion} compiled into /usr/bin/caddy`,
  };
}

export function caddyComponent(version: string, sources: readonly SourceFile[], info: GoBuildInfo, table: TextTable): Component {
  const licence = sources.find((file) => file.path === "LICENSE");
  if (licence === undefined) throw new Error("Caddy's source artifact holds no LICENSE at its root");
  const split = splitCopyright(licence.text);
  return {
    purl: `pkg:github/caddyserver/caddy@v${version}`,
    kind: "runtime",
    name: "Caddy",
    version: `v${version}`,
    licence: identifyLicence(split.body) ?? noAssertion,
    copyright: split.copyright,
    texts: [table.add(identifyLicence(split.body) ?? "the licence of Caddy", split.body)],
    notices: [],
    inImage: [],
    source: `https://github.com/caddyserver/caddy/tree/v${version}`,
    detail: `the server /usr/bin/caddy of the Caddy base image, built from commit ${info.settings["vcs.revision"] ?? "unknown"} with ${info.goVersion}; its Go modules follow`,
  };
}
