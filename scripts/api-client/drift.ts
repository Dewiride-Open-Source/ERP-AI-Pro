import { mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, posix, relative, sep } from "node:path";

import { compareTrees, fail, generate, generatedRoot, readTree, repoRoot } from "./lib/kiota.ts";

const workspace = mkdtempSync(join(tmpdir(), "erp-api-client-"));

try {
  const first = join(workspace, "first");
  const second = join(workspace, "second");
  generate(first);
  generate(second);

  const firstTree = readTree(first);
  const nondeterministic = compareTrees(firstTree, readTree(second));
  if (nondeterministic.length > 0) {
    report("kiota produced different output from the same document on two runs (microsoft/kiota#7997); the committed client cannot be checked:", nondeterministic);
  }

  const drift = compareTrees(firstTree, readTree(generatedRoot));
  if (drift.length > 0) {
    report(`${relative(repoRoot, generatedRoot).split(sep).join(posix.sep)} differs from what docs/openapi/erp.json generates. Run 'node scripts/api-client/generate.ts' and commit the result:`, drift);
  }

  console.log(`api client ok: ${firstTree.size} generated files match docs/openapi/erp.json`);
} finally {
  rmSync(workspace, { recursive: true, force: true });
}

function report(heading: string, differences: string[]): never {
  console.error(heading);
  for (const difference of differences) console.error(`  ${difference}`);
  rmSync(workspace, { recursive: true, force: true });
  fail(`${differences.length} generated file(s) differ`);
}
