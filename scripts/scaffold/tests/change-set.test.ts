import assert from "node:assert/strict";
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";

import { ChangeSet } from "../lib/change-set.ts";

function withRoot(body: (root: string) => Promise<void> | void): () => Promise<void> {
  return async () => {
    const root = mkdtempSync(join(tmpdir(), "scaffold-changes-"));
    try {
      await body(root);
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  };
}

test(
  "a plan creates files in new folders and edits existing ones, a later edit of a planned file building on the earlier one",
  withRoot(async (root) => {
    writeFileSync(join(root, "list.txt"), "a\n");
    const changes = new ChangeSet(root);
    changes.create("Modules/Jobs/README.md", "Jobs");
    changes.edit("list.txt", (text) => `${text}b\n`);
    changes.edit("list.txt", (text) => `${text}c\n`);

    await changes.apply();

    assert.equal(readFileSync(join(root, "Modules", "Jobs", "README.md"), "utf8"), "Jobs");
    assert.equal(readFileSync(join(root, "list.txt"), "utf8"), "a\nb\nc\n");
    assert.deepEqual(changes.changes, [
      { path: "Modules/Jobs/README.md", kind: "create" },
      { path: "list.txt", kind: "edit" },
    ]);
  }),
);

test(
  "every refusal is listed and a refused plan writes nothing",
  withRoot(async (root) => {
    writeFileSync(join(root, "taken.txt"), "x");
    const changes = new ChangeSet(root);
    changes.create("new.txt", "new");
    changes.create("taken.txt", "y");
    changes.edit("missing.txt", (text) => text);
    changes.edit("taken.txt", () => {
      throw new Error("the anchor is missing");
    });
    changes.create("../outside.txt", "z");

    assert.deepEqual(changes.problems, ["taken.txt already exists", "missing.txt does not exist", "taken.txt: the anchor is missing", "../outside.txt lies outside " + root]);
    await assert.rejects(() => changes.apply(), /the plan was refused/);
    assert.equal(existsSync(join(root, "new.txt")), false);
  }),
);

test(
  "a write that fails part of the way restores the edited files and removes the created files and folders",
  withRoot(async (root) => {
    writeFileSync(join(root, "list.txt"), "original");
    writeFileSync(join(root, "blocker"), "a file where a folder is needed");
    const changes = new ChangeSet(root);
    changes.edit("list.txt", () => "changed");
    changes.create("Modules/Jobs/README.md", "Jobs");
    changes.create("blocker/inside.txt", "cannot be written");

    await assert.rejects(() => changes.apply());

    assert.equal(readFileSync(join(root, "list.txt"), "utf8"), "original");
    assert.equal(existsSync(join(root, "Modules")), false);
    assert.equal(readFileSync(join(root, "blocker"), "utf8"), "a file where a folder is needed");
  }),
);
