import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { join, resolve } from "node:path";

import { decodeText, type Finding, isScannable, scanText, textEncoding } from "./lib/secret-patterns.ts";
import { repoRoot } from "./lib/walk.ts";

const selfPaths = ["scripts/checks/secret-patterns.ts", "scripts/checks/lib/secret-patterns.ts", "scripts/checks/tests/secret-patterns.test.ts"];

const rootArgument = process.argv.indexOf("--root");
const root = rootArgument === -1 ? repoRoot : resolve(process.argv[rootArgument + 1] ?? repoRoot);

const files = execFileSync("git", ["-C", root, "ls-files", "-z", "--cached", "--others", "--exclude-standard"], {
  encoding: "utf8",
  maxBuffer: 64 * 1024 * 1024,
})
  .split("\0")
  .filter((path) => path !== "")
  .filter((path) => !selfPaths.includes(path));

const findings: Finding[] = [];
let scanned = 0;

for (const repoPath of files) {
  let content: Buffer;
  try {
    content = readFileSync(join(root, repoPath));
  } catch {
    continue;
  }
  if (!isScannable(repoPath, content)) continue;
  scanned += 1;
  findings.push(...scanText(repoPath, decodeText(content, textEncoding(content) ?? "utf8")));
}

if (findings.length > 0) {
  console.error("Secret patterns found (rotate the value, then remove it; never commit it):");
  for (const finding of findings) console.error(`  ${finding.path}:${finding.line}: ${finding.reason} — ${finding.name} ${finding.excerpt}`);
  process.exit(1);
}

console.log(`secret patterns ok: ${scanned} files scanned`);
