import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";

import { renderText, renderTree } from "../lib/templates.ts";

const tokens = new Map([
  ["__Module__", "Jobs"],
  ["__module-kebab__", "jobs"],
  ["__Entity__", "LedgerNote"],
  ["__Entities__", "LedgerNotes"],
]);

test("every token is replaced, and text that only resembles a token is left alone", () => {
  assert.equal(
    renderText("class __Entity__Endpoints in __Entities__; Erp__Platform__Database__ConnectionString; feature_management__feature_flags__0__id", tokens, "a"),
    "class LedgerNoteEndpoints in LedgerNotes; Erp__Platform__Database__ConnectionString; feature_management__feature_flags__0__id",
  );
});

test("a token of the scaffolder's vocabulary that the input does not define is refused, naming the template", () => {
  assert.throws(() => renderText("__Entity-kebab__", tokens, "web/x.ts.tmpl"), /web\/x\.ts\.tmpl: __Entity-kebab__ is no token of the scaffolder/);
  assert.throws(() => renderText("__AreaTitle__", tokens, "a"), /__AreaTitle__ is no token/);
});

test("a tree renders its paths and contents under the output root, dropping the template suffix", () => {
  const root = mkdtempSync(join(tmpdir(), "scaffold-templates-"));
  try {
    mkdirSync(join(root, "templates", "__Entities__"), { recursive: true });
    writeFileSync(join(root, "templates", "__Module__Module.cs.tmpl"), "namespace __Module__;");
    writeFileSync(join(root, "templates", "__Entities__", "__Entity__.cs.tmpl"), "class __Entity__;");

    const files = renderTree(join(root, "templates"), join(root, "out"), tokens);

    assert.deepEqual(
      files.map(({ output, contents }) => [output, contents]),
      [
        [join(root, "out", "JobsModule.cs"), "namespace Jobs;"],
        [join(root, "out", "LedgerNotes", "LedgerNote.cs"), "class LedgerNote;"],
      ],
    );
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("a file without the template suffix in a template folder is refused", () => {
  const root = mkdtempSync(join(tmpdir(), "scaffold-templates-"));
  try {
    mkdirSync(join(root, "templates"));
    writeFileSync(join(root, "templates", "README.md"), "stray");
    assert.throws(() => renderTree(join(root, "templates"), join(root, "out"), tokens), /README\.md: a template folder holds only \.tmpl files/);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
