import { dirname, join } from "node:path";

import { hasSegment, repoRoot, toRepoPath, walk } from "./lib/walk.ts";

const MAX_SOURCE_FILES_PER_FOLDER = 12;
const sourceExtensions = [".cs", ".ts", ".tsx"];
const generatedFolders = ["Migrations"];
const generatedPaths = ["/components/ui", "/packages/api-client/src/generated"];

const roots = [join(repoRoot, "backend"), join(repoRoot, "frontend"), join(repoRoot, "scripts")];

const counts = new Map<string, number>();
for (const root of roots) {
  for (const file of walk(root, (f) => sourceExtensions.some((ext) => f.endsWith(ext)))) {
    const repoPath = toRepoPath(file);
    if (repoPath.endsWith(".d.ts")) continue;
    const folder = dirname(repoPath);
    if (isGenerated(folder)) continue;
    counts.set(folder, (counts.get(folder) ?? 0) + 1);
  }
}

const offenders = [...counts.entries()]
  .filter(([, count]) => count > MAX_SOURCE_FILES_PER_FOLDER)
  .sort(([a], [b]) => a.localeCompare(b));

if (offenders.length > 0) {
  console.error(`Folders with more than ${MAX_SOURCE_FILES_PER_FOLDER} source files (split by sub-feature or concern):`);
  for (const [folder, count] of offenders) console.error(`  ${count.toString().padStart(3)}  ${folder}`);
  process.exit(1);
}

function isGenerated(folder: string): boolean {
  return generatedFolders.some((name) => hasSegment(folder, name)) || generatedPaths.some((path) => folder.includes(path));
}

console.log(`folder cap ok: ${counts.size} folders, none above ${MAX_SOURCE_FILES_PER_FOLDER} source files`);
