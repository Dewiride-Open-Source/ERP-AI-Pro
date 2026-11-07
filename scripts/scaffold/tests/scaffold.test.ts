import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";

import { ignoredProblems } from "../lib/scaffold.ts";

function withRepository(body: (root: string) => void): () => void {
  return () => {
    const root = mkdtempSync(join(tmpdir(), "scaffold-ignored-"));
    try {
      const init = spawnSync("git", ["init", "--quiet"], { cwd: root, encoding: "utf8", shell: false });
      assert.equal(init.status, 0, init.stderr);
      body(root);
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  };
}

test(
  "a planned file git ignores is reported by its rule, files under one rule together, and a file a negated rule keeps is not",
  withRepository((root) => {
    writeFileSync(join(root, ".gitignore"), "*.log\n!kept.log\nTestResults/\n");
    const problems = ignoredProblems(root, ["kept.log", "dropped.log", "Feature/TestResults/One.cs", "Feature/TestResults/Two.cs", "Feature/Other.cs"]);
    assert.deepEqual(problems, [
      "dropped.log matches the git ignore rule *.log (.gitignore:1), so a commit would leave it out; choose other names",
      "Feature/TestResults/One.cs and 1 more planned files match the git ignore rule TestResults/ (.gitignore:3), so a commit would leave them out; choose other names",
    ]);
  }),
);

test(
  "no planned file means no question to git, and files git keeps raise nothing",
  withRepository((root) => {
    writeFileSync(join(root, ".gitignore"), "TestResults/\n");
    assert.deepEqual(ignoredProblems(root, []), []);
    assert.deepEqual(ignoredProblems(root, ["Feature/Domain/Note.cs"]), []);
  }),
);
