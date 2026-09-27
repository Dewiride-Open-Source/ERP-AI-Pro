import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { mkdirSync, mkdtempSync, rmSync, unlinkSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { after, before, test } from "node:test";

import { readWebSource } from "../lib/web-source.ts";

let root = "";

const write = (path: string, content: string) => {
  mkdirSync(dirname(join(root, path)), { recursive: true });
  writeFileSync(join(root, path), content);
};

const git = (...args: string[]) => execFileSync("git", ["-C", root, ...args], { stdio: "ignore" });

before(() => {
  root = mkdtempSync(join(tmpdir(), "erp-web-source-"));
  git("init", "--quiet");
  write(".gitignore", "*.log\n");
  write("app/(app)/platform/dist/helper.tsx", "export const helper = 1;\n");
  write("features/platform/artifacts/index.ts", 'export { Page } from "./files/components/page";\n');
  write("app/icon.svg", "<svg />");
  write("app/debug.log", "ignored");
  write("shared/layout/removed.tsx", "export const removed = 1;\n");
  git("add", "shared/layout/removed.tsx");
  unlinkSync(join(root, "shared/layout/removed.tsx"));
});

after(() => {
  rmSync(root, { recursive: true, force: true });
});

test("readWebSource_FoldersNamedLikeBuildOutput_AreRead", () => {
  const paths = readWebSource(root).map((file) => file.path);
  assert.ok(paths.includes("app/(app)/platform/dist/helper.tsx"));
  assert.ok(paths.includes("features/platform/artifacts/index.ts"));
});

test("readWebSource_CodeFile_CarriesItsContent", () => {
  const helper = readWebSource(root).find((file) => file.path === "app/(app)/platform/dist/helper.tsx");
  assert.equal(helper?.content, "export const helper = 1;\n");
});

test("readWebSource_NonCodeFile_IsListedWithoutContent", () => {
  const icon = readWebSource(root).find((file) => file.path === "app/icon.svg");
  assert.equal(icon?.content, "");
});

test("readWebSource_GitIgnoredFile_IsSkipped", () => {
  assert.ok(!readWebSource(root).some((file) => file.path === "app/debug.log"));
});

test("readWebSource_StagedFileDeletedFromDisk_IsSkipped", () => {
  assert.ok(!readWebSource(root).some((file) => file.path === "shared/layout/removed.tsx"));
});
