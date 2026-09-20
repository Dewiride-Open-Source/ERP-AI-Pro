import { spawnSync } from "node:child_process";
import { globSync } from "node:fs";

const pattern = "src/**/*.test.ts";
const files = globSync(pattern).sort((a, b) => a.localeCompare(b));

if (files.length === 0) {
  console.error(`No unit test files match ${pattern}; the suite would pass without running anything.`);
  process.exit(1);
}

const result = spawnSync(process.execPath, ["--test", ...files], { stdio: "inherit" });
process.exit(result.status ?? 1);
