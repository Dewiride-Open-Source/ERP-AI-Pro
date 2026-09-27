#!/usr/bin/env node
import { spawnSync } from "node:child_process";
import { globSync } from "node:fs";

const [pattern = "src/**/*.test.ts", ...extra] = process.argv.slice(2);

if (extra.length > 0) {
  console.error(`erp-unit-tests takes at most one glob; received ${[pattern, ...extra].join(" ")}.`);
  process.exit(1);
}

const files = globSync(pattern).sort((a, b) => a.localeCompare(b));

if (files.length === 0) {
  console.error(`No unit test files match ${pattern}; the suite would pass without running anything.`);
  process.exit(1);
}

const sourceAliasHooks = new URL("./resolve-source-alias.mjs", import.meta.url).href;

const result = spawnSync(process.execPath, ["--import", sourceAliasHooks, "--test", ...files], {
  stdio: "inherit",
});
process.exit(result.status ?? 1);
