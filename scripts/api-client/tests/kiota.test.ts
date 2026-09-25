import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";

import { compareTrees, documentPath, generateArguments, generatedRoot, lockFileName, logFileName, normalise, readTree, repoRoot } from "../lib/kiota.ts";

const value = (args: string[], option: string) => args[args.indexOf(option) + 1];

test("the generator writes a TypeScript client named ErpApiClient from the committed document", () => {
  const args = generateArguments("out");

  assert.deepEqual(args.slice(0, 2), ["kiota", "generate"]);
  assert.equal(value(args, "--language"), "typescript");
  assert.equal(value(args, "--openapi"), documentPath);
  assert.equal(value(args, "--class-name"), "ErpApiClient");
  assert.equal(value(args, "--output"), "out");
  assert.equal(documentPath, join(repoRoot, "docs", "openapi", "erp.json"));
  assert.equal(generatedRoot, join(repoRoot, "frontend", "packages", "api-client", "src", "generated"));
});

test("every run starts from an empty folder and drops the backward-compatible surface", () => {
  const args = generateArguments("out");

  assert.ok(args.includes("--clean-output"));
  assert.ok(args.includes("--exclude-backward-compatible"));
});

test("only JSON is serialised, and the serializer names never start a token with @", () => {
  const args = generateArguments("out");

  assert.equal(value(args, "--structured-mime-types"), "application/json");
  assert.ok(args.includes("--serializer=@microsoft/kiota-serialization-json.JsonSerializationWriterFactory"));
  assert.ok(args.includes("--deserializer=@microsoft/kiota-serialization-json.JsonParseNodeFactory"));
  assert.ok(args.every((arg) => !arg.startsWith("@")));
});

test("the missing server entry is accepted because the web app sets the base URL itself", () => {
  const args = generateArguments("out");

  assert.equal(value(args, "--disable-validation-rules"), "NoServerEntry");
});

test("a document path can be supplied for a run against another description", () => {
  assert.equal(value(generateArguments("out", "other.json"), "--openapi"), "other.json");
});

test("identical trees have no differences", () => {
  const tree = new Map([["a.ts", "x"], ["b/c.ts", "y"]]);

  assert.deepEqual(compareTrees(tree, new Map(tree)), []);
});

test("missing, unexpected and changed files are each reported by path", () => {
  const expected = new Map([["a.ts", "x"], ["b.ts", "y"], ["c.ts", "z"]]);
  const actual = new Map([["a.ts", "x"], ["b.ts", "changed"], ["d.ts", "new"]]);

  assert.deepEqual(compareTrees(expected, actual), ["b.ts: content differs", "c.ts: missing", "d.ts: unexpected"]);
});

test("the lock file is compared without the folder-relative document location", () => {
  const lock = (location: string) => JSON.stringify({ descriptionHash: "AB", descriptionLocation: location, kiotaVersion: "1.35.0" }, null, 2);

  assert.equal(normalise(lockFileName, lock("../../docs/openapi/erp.json")), normalise(lockFileName, lock("../../../other/erp.json")));
  assert.notEqual(
    normalise(lockFileName, JSON.stringify({ descriptionHash: "AB" })),
    normalise(lockFileName, JSON.stringify({ descriptionHash: "CD" })),
  );
});

test("files other than the lock file are compared exactly", () => {
  assert.equal(normalise("models/index.ts", "export {};\n"), "export {};\n");
});

test("a tree is read with posix paths and LF endings and without the kiota log", () => {
  const root = mkdtempSync(join(tmpdir(), "erp-api-client-test-"));
  try {
    mkdirSync(join(root, "models"));
    writeFileSync(join(root, "models", "index.ts"), "line one\r\nline two\r\n");
    writeFileSync(join(root, logFileName), "Information: C:\\machine\\specific\\path");
    writeFileSync(join(root, "erpApiClient.ts"), "export {};\n");

    const tree = readTree(root);

    assert.deepEqual([...tree.keys()], ["erpApiClient.ts", "models/index.ts"]);
    assert.equal(tree.get("models/index.ts"), "line one\nline two\n");
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("a folder that does not exist reads as an empty tree", () => {
  assert.equal(readTree(join(tmpdir(), "erp-api-client-absent-folder")).size, 0);
});
