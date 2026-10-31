import assert from "node:assert/strict";
import { test } from "node:test";
import { gzipSync } from "node:zlib";

import {
  collect,
  documentationFolder,
  expectedLabels,
  finalFileSystem,
  forbiddenProblems,
  imageContentsProblems,
  isRequiredFile,
  labelProblems,
  type Layer,
  listedPathsPerRule,
  readFirstTarEntry,
  readImageArchive,
  readTar,
  requiredFiles,
  requiredProblems,
  type TarEntry,
} from "../lib/image-contents.ts";

type HeaderFields = { name: string; type?: string; mode?: number; uid?: number; gid?: number; size?: number; linkName?: string; prefix?: string; magic?: string; modeField?: string; uidBytes?: number[] };

const encoder = new TextEncoder();

function header(fields: HeaderFields): Uint8Array {
  const block = new Uint8Array(512);
  const write = (offset: number, value: string) => block.set(encoder.encode(value), offset);
  const octal = (offset: number, length: number, value: number) => write(offset, `${value.toString(8).padStart(length - 1, "0")}\0`);
  write(0, fields.name);
  if (fields.modeField === undefined) octal(100, 8, fields.mode ?? 0o644);
  else write(100, fields.modeField);
  if (fields.uidBytes === undefined) octal(108, 8, fields.uid ?? 0);
  else block.set(fields.uidBytes, 108);
  octal(116, 8, fields.gid ?? 0);
  octal(124, 12, fields.size ?? 0);
  octal(136, 12, 0);
  write(156, fields.type ?? "0");
  if (fields.linkName) write(157, fields.linkName);
  write(257, fields.magic ?? "ustar\u000000");
  if (fields.prefix) write(345, fields.prefix);
  block.fill(0x20, 148, 156);
  const sum = block.reduce((total, byte) => total + byte, 0);
  write(148, `${sum.toString(8).padStart(6, "0")}\0 `);
  return block;
}

function entry(fields: HeaderFields, data: Uint8Array | string = new Uint8Array(0)): Uint8Array {
  const bytes = typeof data === "string" ? encoder.encode(data) : data;
  const padding = (512 - (bytes.length % 512)) % 512;
  return Buffer.concat([header({ ...fields, size: fields.size ?? bytes.length }), bytes, new Uint8Array(padding)]);
}

function pax(records: Record<string, string>, type = "x"): Uint8Array {
  const body = Object.entries(records)
    .map(([key, value]) => {
      const record = ` ${key}=${value}\n`;
      let length = record.length + 1;
      while (`${length}${record}`.length !== length) length += 1;
      return `${length}${record}`;
    })
    .join("");
  return entry({ name: "PaxHeaders/entry", type }, body);
}

const archive = (...entries: Uint8Array[]): Uint8Array => Buffer.concat([...entries, new Uint8Array(1024)]);

async function* chunks(bytes: Uint8Array, size = 97): AsyncGenerator<Uint8Array> {
  for (let offset = 0; offset < bytes.length; offset += size) yield bytes.subarray(offset, offset + size);
}

async function entriesOf(bytes: Uint8Array): Promise<{ name: string; type: string; size: number; linkName: string; uid: number; text: string }[]> {
  const read = [];
  for await (const { header: item, content } of readTar(chunks(bytes))) {
    read.push({ name: item.name, type: item.type, size: item.size, linkName: item.linkName, uid: item.uid, text: new TextDecoder().decode(await collect(content)) });
  }
  return read;
}

const keyRingFolder: TarEntry = { name: "DataProtection-Keys/", type: "5", mode: 0o700, uid: 1654, gid: 1654 };

test("readFirstTarEntry reads the name, type, mode and owner of the first ustar header", () => {
  assert.deepEqual(readFirstTarEntry(header(keyRingFolder)), keyRingFolder);
  assert.deepEqual(readFirstTarEntry(header({ ...keyRingFolder, magic: "ustar  \0" })), keyRingFolder);
  assert.deepEqual(readFirstTarEntry(header({ ...keyRingFolder, modeField: "   700 \0" })), keyRingFolder);
  assert.equal(readFirstTarEntry(header({ ...keyRingFolder, uidBytes: [0x80, 0, 0, 0, 0, 0, 0x27, 0x11] })).uid, 10001);
});

test("readFirstTarEntry joins the ustar prefix to the name", () => {
  assert.equal(readFirstTarEntry(header({ ...keyRingFolder, prefix: "home/app/.local/share/ERP-AI-Pro" })).name, "home/app/.local/share/ERP-AI-Pro/DataProtection-Keys/");
});

test("readFirstTarEntry refuses a stream that is shorter than a header, not ustar, fails its checksum or holds a non-octal number", () => {
  assert.throws(() => readFirstTarEntry(new Uint8Array(100)), /holds 100 bytes, less than one tar header/);
  assert.throws(() => readFirstTarEntry(header({ ...keyRingFolder, magic: "\0\0\0\0\0\0" })), /does not start with a ustar header/);
  const tampered = header(keyRingFolder);
  tampered[0] = 0x65;
  assert.throws(() => readFirstTarEntry(tampered), /fails its checksum/);
  assert.throws(() => readFirstTarEntry(header({ ...keyRingFolder, modeField: "0000789\0" })), /field at byte 100 is not an octal number \("0000789"\)/);
  assert.throws(() => readFirstTarEntry(header({ ...keyRingFolder, modeField: "\0" })), /field at byte 100 is not an octal number \(""\)/);
});

test("readTar streams each entry's content and applies pax records, global records and GNU long names", async () => {
  const longName = `app/${"deep/".repeat(30)}file.txt`;
  const bytes = archive(
    entry({ name: "etc/plain.txt" }, "plain"),
    pax({ path: "usr/share/doc/erp-ai-pro/THIRD-PARTY-NOTICES.txt", uid: "10001" }),
    entry({ name: "short-name" }, "notices"),
    entry({ name: "././@LongLink", type: "L" }, `${longName}\0`),
    entry({ name: "truncated" }, "long"),
    entry({ name: "././@LongLink", type: "K" }, "../target/of/a/long/link\0"),
    entry({ name: "link", type: "2", linkName: "short" }),
    pax({ comment: "every entry" }, "g"),
    entry({ name: "var/empty", type: "5", mode: 0o755 }),
  );
  assert.deepEqual(await entriesOf(bytes), [
    { name: "etc/plain.txt", type: "0", size: 5, linkName: "", uid: 0, text: "plain" },
    { name: "usr/share/doc/erp-ai-pro/THIRD-PARTY-NOTICES.txt", type: "0", size: 7, linkName: "", uid: 10001, text: "notices" },
    { name: longName, type: "0", size: 4, linkName: "", uid: 0, text: "long" },
    { name: "link", type: "2", size: 0, linkName: "../target/of/a/long/link", uid: 0, text: "" },
    { name: "var/empty", type: "5", size: 0, linkName: "", uid: 0, text: "" },
  ]);
});

test("readTar skips the content a reader leaves unread and refuses a stream that ends inside an entry", async () => {
  const bytes = archive(entry({ name: "a" }, "x".repeat(1500)), entry({ name: "b" }, "second"));
  const names = [];
  for await (const { header: item } of readTar(chunks(bytes))) names.push(item.name);
  assert.deepEqual(names, ["a", "b"]);
  await assert.rejects(entriesOf(entry({ name: "a", size: 2048 }, "x".repeat(10)).subarray(0, 700)), /ends inside an entry/);
  await assert.rejects(entriesOf(new Uint8Array(300).fill(1)), /ends inside a header/);
});

const sha = (seed: string) => seed.repeat(64).slice(0, 64);

function savedImage(options: { labels?: Record<string, string>; layers: Uint8Array[]; legacyLink?: boolean }): Uint8Array {
  const config = { created: "2026-10-28T04:00:00Z", config: { Labels: options.labels ?? {}, User: "app" } };
  const layerPaths = options.layers.map((_, index) => `blobs/sha256/${sha(String(index + 1))}`);
  const files = options.layers.map((layer, index) => entry({ name: layerPaths[index] as string }, layer));
  const manifestLayers = options.legacyLink ? [...layerPaths, "legacy/layer.tar"] : layerPaths;
  return archive(
    entry({ name: "blobs/", type: "5" }),
    ...files,
    ...(options.legacyLink ? [entry({ name: "legacy/layer.tar", type: "2", linkName: `../${layerPaths[0]}` })] : []),
    entry({ name: `blobs/sha256/${sha("c")}` }, JSON.stringify(config)),
    entry({ name: "index.json" }, '{"schemaVersion":2}'),
    entry({ name: "manifest.json" }, JSON.stringify([{ Config: `blobs/sha256/${sha("c")}`, RepoTags: ["erp:test"], Layers: manifestLayers }])),
  );
}

const releaseValues = { "org.opencontainers.image.version": "0.1.0", "org.opencontainers.image.revision": "a".repeat(40), "org.opencontainers.image.created": "2026-10-28T04:00:00Z" };

test("readImageArchive reads the configuration and the layers in manifest order, whatever their compression", async () => {
  const gzipped = gzipSync(archive(entry({ name: "usr/share/doc/erp-ai-pro/COPYING" }, "GPL"), entry({ name: "app/Program.dll" }, "dll")));
  const plain = archive(entry({ name: "./etc/", type: "5" }), entry({ name: "./etc/os-release" }, "ID=ubuntu"));
  const empty = new Uint8Array(1024);
  const read = await readImageArchive(chunks(savedImage({ labels: { a: "b" }, layers: [gzipped, plain, empty], legacyLink: true }), 4096), (path) => path.endsWith("COPYING"));
  assert.deepEqual(read.config.Labels, { a: "b" });
  assert.equal(read.created, "2026-10-28T04:00:00Z");
  assert.equal(read.configDigest, `sha256:${sha("c")}`);
  assert.deepEqual(
    read.layers.map((layer) => layer.entries.map(({ path }) => path)),
    [["usr/share/doc/erp-ai-pro/COPYING", "app/Program.dll"], ["etc", "etc/os-release"], [], ["usr/share/doc/erp-ai-pro/COPYING", "app/Program.dll"]],
  );
  assert.deepEqual([...(read.layers[0] as Layer).contents.keys()], ["usr/share/doc/erp-ai-pro/COPYING"]);
});

test("readImageArchive refuses an archive without a manifest, with several images or with a layer it cannot read", async () => {
  await assert.rejects(readImageArchive(chunks(archive(entry({ name: "index.json" }, "{}"))), () => false), /holds no manifest\.json/);
  const twoImages = archive(entry({ name: "manifest.json" }, JSON.stringify([{ Config: "c", Layers: [] }, { Config: "d", Layers: [] }])));
  await assert.rejects(readImageArchive(chunks(twoImages), () => false), /describes 2 images, not one/);
  const missingLayer = archive(entry({ name: "c.json" }, "{}"), entry({ name: "manifest.json" }, JSON.stringify([{ Config: "c.json", Layers: ["l/layer.tar"] }])));
  await assert.rejects(readImageArchive(chunks(missingLayer), () => false), /lacks the layer l\/layer\.tar/);
});

const layerOf = (paths: string[], contents: Record<string, string> = {}): Layer => ({
  blob: "blob",
  entries: paths.map((path) => ({ path: path.replace(/\/$/, ""), type: path.endsWith("/") ? "5" : "0", size: (contents[path] ?? "x").length, linkName: "" })),
  contents: new Map(Object.entries(contents).map(([path, text]) => [path, encoder.encode(text)])),
});

test("finalFileSystem applies whiteouts and opaque whiteouts of upper layers to lower layers only", () => {
  const files = finalFileSystem([
    layerOf(["app/", "app/a.dll", "app/b.dll", "opt/", "opt/tool/", "opt/tool/run", "srv/", "srv/old"]),
    layerOf(["app/.wh.a.dll", "opt/.wh.tool", "srv/.wh..wh..opq", "srv/new", "app/c.dll"]),
    layerOf(["app/.wh.c.dll", "app/c.dll"]),
  ]);
  assert.deepEqual([...files.keys()].sort(), ["app", "app/b.dll", "app/c.dll", "opt", "srv", "srv/new"]);
  assert.equal(files.get("app/c.dll")?.layer, 2);
});

test("every forbidden path is reported in whichever layer holds it, even when an upper layer hides it", () => {
  const problems = forbiddenProblems([
    layerOf([
      "app/runtimes/linux-x64/native/libmsalruntime.so",
      "app/Microsoft.Identity.Client.NativeInterop.dll",
      "app/node_modules/.pnpm/@img+sharp-libvips-linux-x64@1.3.4/node_modules/@img/sharp-libvips-linux-x64/lib/libvips-cpp.so.8.18.7",
      "app/node_modules/.pnpm/@img+sharp-linux-x64@0.35.5/lib/sharp-linux-x64-0.35.5.node",
      "app/node_modules/.pnpm/node_modules/sharp",
      "app/node_modules/.pnpm/@img+colour@1.1.0/node_modules/@img/colour/index.cjs",
    ]),
    layerOf(["app/runtimes/linux-x64/native/.wh.libmsalruntime.so"]),
  ]);
  assert.deepEqual(problems, [
    "layer 1 holds /app/runtimes/linux-x64/native/libmsalruntime.so: the MSAL native runtime, whose licence forbids distributing it",
    "layer 1 holds /app/Microsoft.Identity.Client.NativeInterop.dll: the MSAL native interop library, whose licence forbids distributing it",
    "layer 1 holds /app/node_modules/.pnpm/@img+sharp-libvips-linux-x64@1.3.4/node_modules/@img/sharp-libvips-linux-x64/lib/libvips-cpp.so.8.18.7: libvips, an LGPL library of sharp that the web image keeps out",
    "layer 1 holds /app/node_modules/.pnpm/@img+sharp-linux-x64@0.35.5/lib/sharp-linux-x64-0.35.5.node: the sharp native addon",
    "layer 1 holds /app/node_modules/.pnpm/node_modules/sharp: the sharp package",
    "layer 1 holds /app/node_modules/.pnpm/@img+colour@1.1.0/node_modules/@img/colour/index.cjs: a package of sharp's @img scope",
  ]);
});

test("forbidden paths of one rule are listed up to the limit and then counted", () => {
  const paths = Array.from({ length: listedPathsPerRule + 3 }, (_, index) => `app/runtimes/r${index}/native/msalruntime.dll`);
  const problems = forbiddenProblems([layerOf(paths)]);
  assert.equal(problems.length, listedPathsPerRule + 1);
  assert.equal(problems.at(-1), "and 3 more paths of the MSAL native runtime, whose licence forbids distributing it");
});

test("paths that only resemble forbidden ones pass", () => {
  assert.deepEqual(forbiddenProblems([layerOf(["app/sharpness.dll", "app/node_modules/sharpie/index.js", "app/node_modules/.pnpm/imaging/node_modules/img/index.js", "usr/lib/msal.txt"])]), []);
});

const required = Object.fromEntries(requiredFiles.map((path) => [path, `content of ${path}`]));

test("the licence and notices files must be regular, non-empty and equal to the repository's copies", () => {
  const expected = new Map(Object.entries(required).map(([path, text]) => [path, encoder.encode(text)]));
  assert.deepEqual(requiredProblems(finalFileSystem([layerOf(Object.keys(required), required)]), expected), []);
  const changed = { ...required, [`${documentationFolder}/COPYING`]: "another licence" };
  const files = finalFileSystem([layerOf([...Object.keys(required).filter((path) => !path.endsWith(".json")), `${documentationFolder}/third-party-notices.json/`], changed)]);
  assert.deepEqual(requiredProblems(files, expected), [`/${documentationFolder}/COPYING differs from the repository's copy`, `/${documentationFolder}/third-party-notices.json is not a regular file`]);
  assert.deepEqual(requiredProblems(finalFileSystem([layerOf([`${documentationFolder}/COPYING`], { [`${documentationFolder}/COPYING`]: "" })])), [
    `/${documentationFolder}/COPYING is empty`,
    `/${documentationFolder}/COPYING.LESSER is missing`,
    `/${documentationFolder}/THIRD-PARTY-NOTICES.txt is missing`,
    `/${documentationFolder}/third-party-notices.json is missing`,
  ]);
  assert.equal(isRequiredFile(`${documentationFolder}/THIRD-PARTY-NOTICES.txt`, 1), true);
  assert.equal(isRequiredFile("usr/share/doc/other/COPYING", 1), false);
});

test("an image passes with its own labels and the release values or their local defaults", () => {
  for (const image of ["api", "web", "proxy"] as const) {
    assert.deepEqual(labelProblems(image, { ...expectedLabels(image), ...releaseValues }), []);
    assert.deepEqual(labelProblems(image, { ...expectedLabels(image), "org.opencontainers.image.version": "local", "org.opencontainers.image.revision": "unknown", "org.opencontainers.image.created": "" }), []);
  }
  assert.equal(expectedLabels("proxy")["org.opencontainers.image.licenses"], "LGPL-3.0-only AND Apache-2.0");
});

test("a label inherited from a base image, a missing release label and a malformed release value fail", () => {
  assert.deepEqual(
    labelProblems("proxy", {
      ...expectedLabels("proxy"),
      "org.opencontainers.image.vendor": "Light Code Labs",
      "org.opencontainers.image.version": "v2.11.7",
      "org.opencontainers.image.revision": "main",
      "org.opencontainers.image.base.name": "caddy",
    }),
    [
      'label org.opencontainers.image.vendor: expected "Dewiride Technologies Private Limited", got "Light Code Labs"',
      'label org.opencontainers.image.version: expected local or a version such as 1.2.3, got "v2.11.7"',
      'label org.opencontainers.image.revision: expected unknown or a 40-character commit, got "main"',
      "label org.opencontainers.image.created: missing",
      'label org.opencontainers.image.base.name: not set by this image ("caddy"), so it describes a base image',
    ],
  );
});

test("imageContentsProblems combines the forbidden paths, the required files and the labels of a saved image", async () => {
  const layer = archive(...Object.entries(required).map(([path, text]) => entry({ name: path }, text)), entry({ name: "app/libvips.so.42" }, "so"));
  const read = await readImageArchive(chunks(savedImage({ labels: { ...expectedLabels("web"), ...releaseValues }, layers: [layer] })), isRequiredFile);
  const expected = new Map(Object.entries(required).map(([path, text]) => [path, encoder.encode(text)]));
  assert.deepEqual(imageContentsProblems("web", read, expected), ["layer 1 holds /app/libvips.so.42: libvips, an LGPL library of sharp that the web image keeps out"]);
  assert.match(imageContentsProblems("api", read, expected).join("\n"), /org\.opencontainers\.image\.title: expected "ERP-AI-Pro API"/);
});
