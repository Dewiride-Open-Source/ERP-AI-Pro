import { spawn } from "node:child_process";
import { readFileSync } from "node:fs";
import { join, posix } from "node:path";
import { Readable, pipeline } from "node:stream";
import { createGunzip, createZstdDecompress } from "node:zlib";

export type ImageName = "api" | "web" | "proxy";

export const imageNames: readonly ImageName[] = ["api", "web", "proxy"];

export const imageRepository = (image: ImageName): string => `ghcr.io/dewiride-open-source/erp-ai-pro/${image}`;

export const isImageName = (value: string | undefined): value is ImageName => imageNames.some((image) => image === value);

export type TarEntry = { name: string; type: string; mode: number; uid: number; gid: number };

export type TarHeader = TarEntry & { size: number; linkName: string };

export type TarItem = { header: TarHeader; content: AsyncIterable<Uint8Array> };

export type LayerEntry = { path: string; type: string; size: number; linkName: string };

export type Layer = { blob: string; entries: LayerEntry[]; contents: Map<string, Uint8Array> };

export type ImageConfig = { Labels?: Record<string, string> | null; Env?: string[] | null; User?: string };

export type ImageArchive = { config: ImageConfig; created: string | undefined; configDigest: string; layers: Layer[] };

export type FileSystemEntry = LayerEntry & { layer: number; content: Uint8Array | undefined };

export type WantedContent = (path: string, size: number) => boolean;

const blockSize = 512;
const regularTypes = new Set(["0", "\0", "7"]);
const typesWithoutData = new Set(["1", "2", "3", "4", "5", "6"]);
const largestBufferedBlob = 16 * 1024 * 1024;
const whiteoutPrefix = ".wh.";
const opaqueWhiteout = ".wh..wh..opq";

function tarText(header: Uint8Array, offset: number, length: number): string {
  const field = header.subarray(offset, offset + length);
  const end = field.indexOf(0);
  return new TextDecoder().decode(end === -1 ? field : field.subarray(0, end));
}

function tarNumber(header: Uint8Array, offset: number, length: number): number {
  const first = header[offset] ?? 0;
  if ((first & 0x80) !== 0) {
    if (first === 0xff) throw new Error(`the tar header field at byte ${offset} holds a negative base-256 number`);
    let value = first & 0x7f;
    for (let index = 1; index < length; index += 1) value = value * 256 + (header[offset + index] ?? 0);
    if (!Number.isSafeInteger(value)) throw new Error(`the tar header field at byte ${offset} holds a base-256 number too large to read`);
    return value;
  }
  const digits = tarText(header, offset, length).trim();
  if (!/^[0-7]+$/.test(digits)) throw new Error(`the tar header field at byte ${offset} is not an octal number ("${digits}")`);
  return Number.parseInt(digits, 8);
}

function isUstar(header: Uint8Array): boolean {
  return tarText(header, 257, 6).startsWith("ustar");
}

function checksumMatches(header: Uint8Array): boolean {
  const stored = tarText(header, 148, 8).trim();
  if (!/^[0-7]+$/.test(stored)) return false;
  let unsigned = 0;
  let signed = 0;
  for (let index = 0; index < blockSize; index += 1) {
    const byte = index >= 148 && index < 156 ? 0x20 : (header[index] ?? 0);
    unsigned += byte;
    signed += byte > 127 ? byte - 256 : byte;
  }
  const expected = Number.parseInt(stored, 8);
  return expected === unsigned || expected === signed;
}

function parseHeader(header: Uint8Array): TarHeader {
  if (!checksumMatches(header)) throw new Error(`the tar header of "${tarText(header, 0, 100)}" fails its checksum`);
  const prefix = tarText(header, 345, 155);
  const name = tarText(header, 0, 100);
  return {
    name: prefix === "" ? name : `${prefix}/${name}`,
    type: String.fromCodePoint(header[156] ?? 0),
    mode: tarNumber(header, 100, 8),
    uid: tarNumber(header, 108, 8),
    gid: tarNumber(header, 116, 8),
    size: tarNumber(header, 124, 12),
    linkName: tarText(header, 157, 100),
  };
}

export function readFirstTarEntry(archive: Uint8Array): TarEntry {
  if (archive.length < blockSize) throw new Error(`the archive holds ${archive.length} bytes, less than one tar header`);
  const header = archive.subarray(0, blockSize);
  if (!isUstar(header)) throw new Error("the archive does not start with a ustar header");
  const { name, type, mode, uid, gid } = parseHeader(header);
  return { name, type, mode, uid, gid };
}

class ByteSource {
  readonly #iterator: AsyncIterator<Uint8Array>;
  #pending: Uint8Array = new Uint8Array(0);
  #ended = false;

  constructor(source: AsyncIterable<Uint8Array>) {
    this.#iterator = source[Symbol.asyncIterator]();
  }

  async #fill(): Promise<boolean> {
    while (this.#pending.length === 0 && !this.#ended) {
      const next = await this.#iterator.next();
      if (next.done === true) this.#ended = true;
      else this.#pending = next.value;
    }
    return this.#pending.length > 0;
  }

  async next(largest: number): Promise<Uint8Array> {
    if (!(await this.#fill())) throw new Error("the tar stream ends inside an entry");
    const piece = this.#pending.subarray(0, Math.min(largest, this.#pending.length));
    this.#pending = this.#pending.subarray(piece.length);
    return piece;
  }

  async take(length: number): Promise<Uint8Array> {
    const pieces: Uint8Array[] = [];
    let taken = 0;
    while (taken < length && (await this.#fill())) {
      const piece = await this.next(length - taken);
      pieces.push(piece);
      taken += piece.length;
    }
    return pieces.length === 1 ? (pieces[0] as Uint8Array) : Buffer.concat(pieces, taken);
  }

  async skip(length: number): Promise<void> {
    let remaining = length;
    while (remaining > 0) remaining -= (await this.next(remaining)).length;
  }

  async drain(): Promise<void> {
    this.#pending = new Uint8Array(0);
    while (!this.#ended) {
      if ((await this.#iterator.next()).done === true) this.#ended = true;
    }
  }
}

function paddingOf(size: number): number {
  return (blockSize - (size % blockSize)) % blockSize;
}

export function paxRecords(data: Uint8Array): Record<string, string> {
  const records: Record<string, string> = {};
  let position = 0;
  while (position < data.length) {
    const space = data.indexOf(0x20, position);
    if (space === -1) throw new Error("a pax extended header holds a record without a length");
    const length = Number.parseInt(new TextDecoder().decode(data.subarray(position, space)), 10);
    if (!Number.isSafeInteger(length) || length <= space - position + 1 || position + length > data.length || data[position + length - 1] !== 0x0a) {
      throw new Error("a pax extended header holds a malformed record");
    }
    const record = new TextDecoder().decode(data.subarray(space + 1, position + length - 1));
    const equals = record.indexOf("=");
    if (equals <= 0) throw new Error("a pax extended header holds a record without a keyword");
    records[record.slice(0, equals)] = record.slice(equals + 1);
    position += length;
  }
  return records;
}

function nulTerminated(data: Uint8Array): string {
  const end = data.indexOf(0);
  return new TextDecoder().decode(end === -1 ? data : data.subarray(0, end));
}

function paxNumber(records: Record<string, string>, key: string, fallback: number): number {
  const value = records[key];
  if (value === undefined) return fallback;
  if (!/^\d+$/.test(value)) throw new Error(`a pax extended header holds a ${key} that is not a number ("${value}")`);
  return Number.parseInt(value, 10);
}

export async function* readTar(source: AsyncIterable<Uint8Array>): AsyncGenerator<TarItem> {
  const bytes = new ByteSource(source);
  let globalRecords: Record<string, string> = {};
  let records: Record<string, string> = {};
  let longName: string | undefined;
  let longLinkName: string | undefined;
  for (;;) {
    const block = await bytes.take(blockSize);
    if (block.length === 0) return;
    if (block.length < blockSize) throw new Error(`the tar stream ends inside a header (${block.length} of ${blockSize} bytes)`);
    if (block.every((byte) => byte === 0)) {
      await bytes.drain();
      return;
    }
    if (!isUstar(block)) throw new Error(`the tar header of "${tarText(block, 0, 100)}" is not a ustar header`);
    const header = parseHeader(block);
    if (header.type === "x" || header.type === "g" || header.type === "L" || header.type === "K") {
      const data = await bytes.take(header.size);
      if (data.length < header.size) throw new Error("the tar stream ends inside an extended header");
      await bytes.skip(paddingOf(header.size));
      if (header.type === "x") records = { ...records, ...paxRecords(data) };
      else if (header.type === "g") globalRecords = { ...globalRecords, ...paxRecords(data) };
      else if (header.type === "L") longName = nulTerminated(data);
      else longLinkName = nulTerminated(data);
      continue;
    }
    const attributes = { ...globalRecords, ...records };
    const size = paxNumber(attributes, "size", header.size);
    const entry: TarHeader = {
      ...header,
      name: longName ?? attributes.path ?? header.name,
      linkName: longLinkName ?? attributes.linkpath ?? header.linkName,
      uid: paxNumber(attributes, "uid", header.uid),
      gid: paxNumber(attributes, "gid", header.gid),
      size,
    };
    records = {};
    longName = undefined;
    longLinkName = undefined;
    const dataLength = typesWithoutData.has(entry.type) ? 0 : size;
    let remaining = dataLength;
    const content = (async function* () {
      while (remaining > 0) {
        const piece = await bytes.next(remaining);
        remaining -= piece.length;
        yield piece;
      }
    })();
    yield { header: entry, content };
    await bytes.skip(remaining);
    await bytes.skip(paddingOf(dataLength));
  }
}

export async function collect(content: AsyncIterable<Uint8Array>): Promise<Uint8Array> {
  const pieces: Uint8Array[] = [];
  for await (const piece of content) pieces.push(piece);
  return Buffer.concat(pieces);
}

async function drain(content: AsyncIterable<Uint8Array>): Promise<void> {
  const iterator = content[Symbol.asyncIterator]();
  while ((await iterator.next()).done !== true);
}

export function normalisedPath(name: string): string {
  return posix.normalize(`/${name}`).slice(1).replace(/\/$/, "");
}

export async function readLayer(source: AsyncIterable<Uint8Array>, blob: string, wanted: WantedContent): Promise<Layer> {
  const entries: LayerEntry[] = [];
  const contents = new Map<string, Uint8Array>();
  for await (const { header, content } of readTar(source)) {
    const path = normalisedPath(header.name);
    entries.push({ path, type: header.type, size: header.size, linkName: header.linkName });
    if (regularTypes.has(header.type) && wanted(path, header.size)) contents.set(path, await collect(content));
  }
  return { blob, entries, contents };
}

async function peek(content: AsyncIterable<Uint8Array>, length: number): Promise<{ head: Uint8Array; rest: AsyncIterable<Uint8Array> }> {
  const iterator = content[Symbol.asyncIterator]();
  const buffered: Uint8Array[] = [];
  let size = 0;
  let done = false;
  while (size < length && !done) {
    const next = await iterator.next();
    if (next.done === true) done = true;
    else {
      buffered.push(next.value);
      size += next.value.length;
    }
  }
  const rest = (async function* () {
    yield* buffered;
    if (done) return;
    for (;;) {
      const next = await iterator.next();
      if (next.done === true) return;
      yield next.value;
    }
  })();
  return { head: Buffer.concat(buffered, size), rest };
}

type BlobKind = "gzip" | "zstd" | "tar" | "other";

function blobKind(head: Uint8Array): BlobKind {
  if (head[0] === 0x1f && head[1] === 0x8b) return "gzip";
  if (head[0] === 0x28 && head[1] === 0xb5 && head[2] === 0x2f && head[3] === 0xfd) return "zstd";
  if (head.length < blockSize) return "other";
  const first = head.subarray(0, blockSize);
  return isUstar(first) || first.every((byte) => byte === 0) ? "tar" : "other";
}

function decompressed(content: AsyncIterable<Uint8Array>, kind: "gzip" | "zstd"): AsyncIterable<Uint8Array> {
  const decompressor = kind === "gzip" ? createGunzip() : createZstdDecompress();
  return pipeline(Readable.from(content), decompressor, () => {});
}

type ArchiveManifest = { Config?: unknown; Layers?: unknown };

type ConfigDocument = { config?: ImageConfig; created?: string };

export async function readImageArchive(source: AsyncIterable<Uint8Array>, wanted: WantedContent): Promise<ImageArchive> {
  const layers = new Map<string, Layer>();
  const documents = new Map<string, Uint8Array>();
  const links = new Map<string, string>();
  let manifest: Uint8Array | undefined;
  for await (const { header, content } of readTar(source)) {
    const name = normalisedPath(header.name);
    if (header.type === "1" || header.type === "2") {
      links.set(name, normalisedPath(header.type === "1" ? header.linkName : posix.join(posix.dirname(name), header.linkName)));
      continue;
    }
    if (!regularTypes.has(header.type)) continue;
    if (name === "manifest.json") {
      manifest = await collect(content);
      continue;
    }
    const { head, rest } = await peek(content, blockSize);
    const kind = blobKind(head);
    if (kind === "other") {
      if (header.size <= largestBufferedBlob) documents.set(name, await collect(rest));
      else await drain(rest);
      continue;
    }
    layers.set(name, await readLayer(kind === "tar" ? rest : decompressed(rest, kind), name, wanted));
  }
  if (manifest === undefined) throw new Error("the image archive holds no manifest.json");
  const entries = JSON.parse(new TextDecoder().decode(manifest)) as ArchiveManifest[];
  if (!Array.isArray(entries) || entries.length !== 1) throw new Error(`the image archive's manifest.json describes ${Array.isArray(entries) ? entries.length : 0} images, not one`);
  const [{ Config: configPath, Layers: layerPaths }] = entries as [ArchiveManifest];
  if (typeof configPath !== "string" || !Array.isArray(layerPaths) || !layerPaths.every((path) => typeof path === "string")) {
    throw new Error("the image archive's manifest.json does not name a config and its layers");
  }
  const resolved = (path: string): string => {
    const normalised = normalisedPath(path);
    return links.get(normalised) ?? normalised;
  };
  const configDocument = documents.get(resolved(configPath));
  if (configDocument === undefined) throw new Error(`the image archive lacks its config ${configPath}`);
  const parsed = JSON.parse(new TextDecoder().decode(configDocument)) as ConfigDocument;
  return {
    config: parsed.config ?? {},
    created: parsed.created,
    configDigest: `sha256:${posix.basename(resolved(configPath)).replace(/\.json$/, "")}`,
    layers: (layerPaths as string[]).map((path) => {
      const layer = layers.get(resolved(path));
      if (layer === undefined) throw new Error(`the image archive lacks the layer ${path}, or it is not a tar archive`);
      return layer;
    }),
  };
}

export class CommandFailure extends Error {
  readonly code: number | null;

  constructor(message: string, code: number | null) {
    super(message);
    this.code = code;
  }
}

export type CommandStream = { stdout: AsyncIterable<Uint8Array>; finished: Promise<void>; stop: () => void };

export function streamCommand(command: string, args: readonly string[]): CommandStream {
  const child = spawn(command, args, { stdio: ["ignore", "pipe", "pipe"] });
  const errors: Buffer[] = [];
  child.stderr.on("data", (chunk: Buffer) => errors.push(chunk));
  const finished = new Promise<void>((resolveFinished, rejectFinished) => {
    child.on("error", rejectFinished);
    child.on("close", (code, signal) => {
      if (code === 0) resolveFinished();
      else rejectFinished(new CommandFailure(`${command} ${args.join(" ")} exited with ${code ?? signal}: ${Buffer.concat(errors).toString("utf8").trim()}`, code));
    });
  });
  return { stdout: child.stdout, finished, stop: () => child.kill() };
}

export async function readCommandArchive<T>(command: string, args: readonly string[], read: (stdout: AsyncIterable<Uint8Array>) => Promise<T>): Promise<T> {
  const stream = streamCommand(command, args);
  let result: T;
  try {
    result = await read(stream.stdout);
  } catch (error) {
    stream.stop();
    const exit = await stream.finished.then(
      () => undefined,
      (failure: unknown) => failure,
    );
    throw exit instanceof CommandFailure && exit.code !== null ? exit : error;
  }
  await stream.finished;
  return result;
}

export function readSavedImage(reference: string, wanted: WantedContent): Promise<ImageArchive> {
  return readCommandArchive("docker", ["save", reference], (stdout) => readImageArchive(stdout, wanted));
}

export function finalFileSystem(layers: readonly Layer[]): Map<string, FileSystemEntry> {
  const files = new Map<string, FileSystemEntry>();
  layers.forEach((layer, index) => {
    for (const entry of layer.entries) {
      const base = posix.basename(entry.path);
      if (!base.startsWith(whiteoutPrefix)) continue;
      const folder = posix.dirname(entry.path) === "." ? "" : posix.dirname(entry.path);
      const hidden = base === opaqueWhiteout ? folder : posix.join(folder, base.slice(whiteoutPrefix.length));
      for (const path of files.keys()) {
        if (base === opaqueWhiteout ? hidden === "" || path.startsWith(`${hidden}/`) : path === hidden || path.startsWith(`${hidden}/`)) files.delete(path);
      }
    }
    for (const entry of layer.entries) {
      if (posix.basename(entry.path).startsWith(whiteoutPrefix) || entry.path === "") continue;
      files.set(entry.path, { ...entry, layer: index, content: layer.contents.get(entry.path) });
    }
  });
  return files;
}

export type ForbiddenRule = { test: (path: string) => boolean; reason: string };

const holdsPackage = (path: string, name: string): boolean => {
  const segments = path.split("/");
  return segments.some((segment, index) => segment === "node_modules" && segments[index + 1] === name);
};

export const forbiddenRules: readonly ForbiddenRule[] = [
  { test: (path) => /msalruntime/i.test(posix.basename(path)), reason: "the MSAL native runtime, whose licence forbids distributing it" },
  {
    test: (path) => posix.basename(path).toLowerCase() === "microsoft.identity.client.nativeinterop.dll",
    reason: "the MSAL native interop library, whose licence forbids distributing it",
  },
  { test: (path) => /libvips/i.test(posix.basename(path)), reason: "libvips, an LGPL library of sharp that the web image keeps out" },
  { test: (path) => /^sharp-.*\.node$/.test(posix.basename(path)), reason: "the sharp native addon" },
  { test: (path) => holdsPackage(path, "sharp"), reason: "the sharp package" },
  { test: (path) => holdsPackage(path, "@img"), reason: "a package of sharp's @img scope" },
];

export const listedPathsPerRule = 10;

export const documentationFolder = "usr/share/doc/erp-ai-pro";

export const licenceFiles = ["COPYING", "COPYING.LESSER"] as const;

export const noticesFiles = ["THIRD-PARTY-NOTICES.txt", "third-party-notices.json"] as const;

export const requiredFiles: readonly string[] = [...licenceFiles, ...noticesFiles].map((file) => `${documentationFolder}/${file}`);

export const repositoryUrl = "https://github.com/Dewiride-Open-Source/ERP-AI-Pro";

export const vendor = "Dewiride Technologies Private Limited";

export const imageDescriptions: Readonly<Record<ImageName, { readonly title: string; readonly description: string; readonly licences: string }>> = {
  api: {
    title: "ERP-AI-Pro API",
    description: "The ERP-AI-Pro API host, with its database migrator and health probe",
    licences: "LGPL-3.0-only",
  },
  web: {
    title: "ERP-AI-Pro web app",
    description: "The ERP-AI-Pro web app, served by the Next.js standalone server",
    licences: "LGPL-3.0-only",
  },
  proxy: {
    title: "ERP-AI-Pro edge proxy",
    description: "The ERP-AI-Pro edge proxy: Caddy with the ERP's site configuration",
    licences: "LGPL-3.0-only AND Apache-2.0",
  },
};

export function expectedLabels(image: ImageName): Record<string, string> {
  const { title, description, licences } = imageDescriptions[image];
  return {
    "org.opencontainers.image.title": title,
    "org.opencontainers.image.description": description,
    "org.opencontainers.image.source": repositoryUrl,
    "org.opencontainers.image.url": repositoryUrl,
    "org.opencontainers.image.documentation": `${repositoryUrl}/tree/main/docs`,
    "org.opencontainers.image.vendor": vendor,
    "org.opencontainers.image.licenses": licences,
  };
}

export const releaseLabels: Readonly<Record<string, { readonly pattern: RegExp; readonly shape: string }>> = {
  "org.opencontainers.image.version": { pattern: /^(?:local|(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*))$/, shape: "local or a version such as 1.2.3" },
  "org.opencontainers.image.revision": { pattern: /^(?:unknown|[0-9a-f]{40})$/, shape: "unknown or a 40-character commit" },
  "org.opencontainers.image.created": { pattern: /^(?:|\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z)$/, shape: "empty or a UTC time such as 2026-10-28T09:30:00Z" },
};

export function labelProblems(image: ImageName, labels: Record<string, string>): string[] {
  const expected = expectedLabels(image);
  const known = new Set<string>([...Object.keys(expected), ...Object.keys(releaseLabels)]);
  return [
    ...Object.entries(expected)
      .filter(([key, value]) => labels[key] !== value)
      .map(([key, value]) => `label ${key}: expected "${value}", got ${labels[key] === undefined ? "none" : `"${labels[key]}"`}`),
    ...Object.entries(releaseLabels).flatMap(([key, { pattern, shape }]) => {
      const value = labels[key];
      if (value === undefined) return [`label ${key}: missing`];
      return pattern.test(value) ? [] : [`label ${key}: expected ${shape}, got "${value}"`];
    }),
    ...Object.keys(labels)
      .filter((key) => key.startsWith("org.opencontainers.image.") && !known.has(key))
      .sort()
      .map((key) => `label ${key}: not set by this image ("${labels[key]}"), so it describes a base image`),
  ];
}

export function forbiddenProblems(layers: readonly Layer[]): string[] {
  const hits = new Map<ForbiddenRule, string[]>();
  layers.forEach((layer, index) => {
    for (const entry of layer.entries) {
      if (posix.basename(entry.path).startsWith(whiteoutPrefix)) continue;
      const rule = forbiddenRules.find((candidate) => candidate.test(entry.path));
      if (rule !== undefined) hits.set(rule, [...(hits.get(rule) ?? []), `layer ${index + 1} holds /${entry.path}`]);
    }
  });
  return forbiddenRules.flatMap((rule) => {
    const paths = hits.get(rule) ?? [];
    const listed = paths.slice(0, listedPathsPerRule).map((path) => `${path}: ${rule.reason}`);
    return paths.length > listedPathsPerRule ? [...listed, `and ${paths.length - listedPathsPerRule} more paths of ${rule.reason}`] : listed;
  });
}

export function requiredProblems(files: ReadonlyMap<string, FileSystemEntry>, expectedContents: ReadonlyMap<string, Uint8Array> = new Map()): string[] {
  return requiredFiles.flatMap((path) => {
    const entry = files.get(path);
    if (entry === undefined) return [`/${path} is missing`];
    if (!regularTypes.has(entry.type)) return [`/${path} is not a regular file`];
    if (entry.size === 0) return [`/${path} is empty`];
    const expected = expectedContents.get(path);
    if (expected === undefined) return [];
    if (entry.content === undefined) return [`/${path} was not read from the image`];
    return Buffer.from(entry.content).equals(expected) ? [] : [`/${path} differs from the repository's copy`];
  });
}

export const isRequiredFile: WantedContent = (path) => requiredFiles.includes(path);

export function repositoryCopies(root: string, image: ImageName): Map<string, Uint8Array> {
  return new Map([
    ...licenceFiles.map((file): [string, Uint8Array] => [`${documentationFolder}/${file}`, readFileSync(join(root, file))]),
    ...noticesFiles.map((file): [string, Uint8Array] => [`${documentationFolder}/${file}`, readFileSync(join(root, "infra", "docker", "notices", image, file))]),
  ]);
}

export function imageContentsProblems(image: ImageName, archive: ImageArchive, expectedContents?: ReadonlyMap<string, Uint8Array>): string[] {
  const files = finalFileSystem(archive.layers);
  return [...forbiddenProblems(archive.layers), ...requiredProblems(files, expectedContents), ...labelProblems(image, archive.config.Labels ?? {})];
}
