import assert from "node:assert/strict";
import { test } from "node:test";

import { goBuildInfo, goModuleComponents, isLegalFile, moduleProxyPath, owningModule, parseModuleInfo } from "../lib/go.ts";
import { canonicalBody, TextTable } from "../lib/licence-texts.ts";

const infoStart = Buffer.from("3077af0c9274080241e1c107e6d618e6", "hex");
const infoEnd = Buffer.from("f932433186182072008242104116d8f2", "hex");

function uvarint(value: number): Buffer {
  const bytes: number[] = [];
  let rest = value;
  while (rest >= 0x80) {
    bytes.push((rest & 0x7f) | 0x80);
    rest = Math.floor(rest / 128);
  }
  bytes.push(rest);
  return Buffer.from(bytes);
}

const moduleInfo = [
  "path\tcaddy",
  "mod\tcaddy\t(devel)\t",
  "dep\tgithub.com/BurntSushi/toml\tv1.6.0\th1:toml=",
  "dep\tgolang.org/x/net\tv0.40.0\th1:net=",
  "=>\tgolang.org/x/net\tv0.41.0\th1:replaced=",
  "build\t-tags=nobadger,nomysql",
  "build\tvcs.revision=72dd0fb067f6d7826c7f79907670ba4a713bfe37",
  "",
].join("\n");

function binary(alignedAt: number): Buffer {
  const header = Buffer.alloc(32);
  Buffer.from("\xff Go buildinf:", "latin1").copy(header);
  header[14] = 8;
  header[15] = 0x2;
  const framed = Buffer.concat([infoStart, Buffer.from(moduleInfo), infoEnd]);
  const blob = Buffer.concat([header, uvarint(8), Buffer.from("go1.26.8"), uvarint(framed.length), framed]);
  const misaligned = Buffer.concat([Buffer.from("\xff Go buildinf:", "latin1"), Buffer.alloc(18)]);
  return Buffer.concat([Buffer.alloc(3), misaligned, Buffer.alloc(alignedAt - 3 - misaligned.length), blob, Buffer.alloc(64)]);
}

test("goBuildInfo reads the Go version, the main module, the dependencies with their replacements and the build settings", () => {
  const info = goBuildInfo(binary(4096));
  assert.equal(info.goVersion, "go1.26.8");
  assert.deepEqual(info.main, { path: "caddy", version: "(devel)", sum: "" });
  assert.deepEqual(info.dependencies, [
    { path: "github.com/BurntSushi/toml", version: "v1.6.0", sum: "h1:toml=" },
    { path: "golang.org/x/net", version: "v0.41.0", sum: "h1:replaced=" },
  ]);
  assert.equal(info.settings["vcs.revision"], "72dd0fb067f6d7826c7f79907670ba4a713bfe37");
  assert.equal(info.settings["-tags"], "nobadger,nomysql");
});

test("goBuildInfo refuses a binary without Go build information and parseModuleInfo one without a main module", () => {
  assert.throws(() => goBuildInfo(Buffer.alloc(1024)), /holds no Go build information/);
  assert.throws(() => parseModuleInfo("go1.26.8", "dep\ta\tv1\t\n"), /names no main module/);
  assert.throws(() => parseModuleInfo("go1.26.8", "mod\tcaddy\t(devel)\t\n=>\ta\tv1\t\n"), /replaces a module it does not name/);
});

test("module paths are escaped for the Go module proxy and files belong to the module with the longest path", () => {
  assert.equal(moduleProxyPath("github.com/BurntSushi/toml"), "github.com/!burnt!sushi/toml");
  const modules = ["go.opentelemetry.io/otel", "go.opentelemetry.io/otel/trace", "golang.org/x/net"];
  assert.equal(owningModule("vendor/go.opentelemetry.io/otel/LICENSE", modules), "go.opentelemetry.io/otel");
  assert.equal(owningModule("vendor/go.opentelemetry.io/otel/trace/LICENSE", modules), "go.opentelemetry.io/otel/trace");
  assert.equal(owningModule("vendor/go.opentelemetry.io/otel/internal/x/LICENSE", modules), "go.opentelemetry.io/otel");
  assert.equal(owningModule("vendor/github.com/dgraph-io/badger/LICENSE", modules), undefined);
  assert.equal(owningModule("LICENSE", modules), undefined);
  assert.deepEqual(["LICENSE", "LICENSE.txt", "COPYING", "UNLICENSE", "NOTICE", "PATENTS", "license-mit"].map(isLegalFile), [true, true, true, true, true, true, true]);
  assert.deepEqual(["main.go", "README.md", "licensing.go"].map(isLegalFile), [false, false, false]);
});

test("each Go module of the binary is listed with the licence its texts identify, its notices and its download location", () => {
  const info = parseModuleInfo("go1.26.8", moduleInfo);
  const sources = [
    { path: "vendor/github.com/BurntSushi/toml/COPYING", text: `The MIT License (MIT)\n\nCopyright (c) 2013 TOML authors\n\n${canonicalBody("MIT")}` },
    { path: "vendor/golang.org/x/net/LICENSE", text: `Copyright 2009 The Go Authors.\n\n${canonicalBody("BSD-3-Clause").replace(/the copyright holder/g, "Google LLC")}` },
    { path: "vendor/golang.org/x/net/PATENTS", text: "Additional IP Rights Grant (Patents)" },
    { path: "vendor/golang.org/x/net/internal/socks/LICENSE", text: `${canonicalBody("Apache-2.0")}\n\nAnd a further licence of its own.\n${canonicalBody("MIT")}` },
  ];
  const table = new TextTable();
  const [toml, net] = goModuleComponents(info, sources, table);
  assert.deepEqual([toml?.licence, toml?.copyright, toml?.purl, toml?.source], ["MIT", ["Copyright (c) 2013 TOML authors"], "pkg:golang/github.com/BurntSushi/toml@v1.6.0", "https://proxy.golang.org/github.com/!burnt!sushi/toml/@v/v1.6.0.zip"]);
  assert.equal(net?.licence, "NOASSERTION");
  assert.equal(net?.texts.length, 2);
  assert.equal(net?.notices.length, 1);
  assert.throws(() => goModuleComponents(info, sources.slice(1), new TextTable()), /holds no licence file for the Go module github\.com\/BurntSushi\/toml/);
});
