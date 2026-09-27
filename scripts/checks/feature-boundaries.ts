import { toRepoPath } from "./lib/walk.ts";
import { importViolations } from "./lib/web-imports.ts";
import { readWebSource, webSourceRoot } from "./lib/web-source.ts";
import { structureViolations } from "./lib/web-structure.ts";

const files = readWebSource();
const violations = [...structureViolations(files), ...importViolations(files)];
const root = toRepoPath(webSourceRoot);

if (violations.length > 0) {
  console.error("Frontend structure and import-boundary violations (rules in docs/architecture/dependency-rules.md):");
  for (const { path, rule, message } of violations) console.error(`  ${root}/${path}: ${rule} ${message}`);
  process.exit(1);
}

console.log(`frontend structure and import boundaries ok: ${files.length} files under ${root}`);
