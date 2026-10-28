import { readFileSync } from "node:fs";
import { join } from "node:path";

import { isScanned, policyFindings } from "./lib/comment-policy.ts";
import { hasSegment, repoRoot, toRepoPath, walk } from "./lib/walk.ts";

const roots = ["backend", "frontend", "scripts", "infra", ".github"].map((r) => join(repoRoot, r));
const generatedFolders = ["Migrations"];
const generatedPaths = ["/components/ui/", "/packages/api-client/src/generated/"];

const findings: string[] = [];

for (const root of roots) {
  for (const file of walk(root, () => true)) {
    const repoPath = toRepoPath(file);
    if (!isScanned(repoPath)) continue;
    if (generatedFolders.some((folder) => hasSegment(repoPath, folder)) || generatedPaths.some((path) => repoPath.includes(path))) continue;
    for (const { line, text, reason } of policyFindings(repoPath, readFileSync(file, "utf8"))) findings.push(`${repoPath}:${line}: ${reason} — ${text.trim()}`);
  }
}

if (findings.length > 0) {
  console.error("Comment policy violations:");
  for (const finding of findings) console.error(`  ${finding}`);
  process.exit(1);
}

console.log("comment policy ok");
