import { readFileSync } from "node:fs";
import { join } from "node:path";

import { hasSegment, repoRoot, toRepoPath, walk } from "./lib/walk.ts";

type Language = { extensions: string[]; line: RegExp[]; blockStart?: RegExp; blockEnd?: RegExp };

const languages: Language[] = [
  { extensions: [".cs"], line: [/^\s*\/\/\/?/], blockStart: /\/\*/, blockEnd: /\*\// },
  { extensions: [".ts", ".tsx", ".mjs", ".js"], line: [/^\s*\/\//], blockStart: /\/\*/, blockEnd: /\*\// },
  { extensions: [".yml", ".yaml", ".sh", ".editorconfig", ".dockerignore", ".gitignore"], line: [/^\s*#/] },
  { extensions: ["Dockerfile"], line: [/^\s*#/] },
  { extensions: [".css"], line: [], blockStart: /\/\*/, blockEnd: /\*\// },
];

const bannedPatterns: { pattern: RegExp; reason: string }[] = [
  { pattern: /\b(TODO|FIXME|HACK|XXX)\b/, reason: "use a roadmap note instead of a task marker" },
  {
    pattern: /\b(moved from|previously|no longer|used to be|replaces the old|updated to|legacy|deprecated)\b/i,
    reason: "comments describe the current code, not its history",
  },
  { pattern: /#region\b/, reason: "regions are not used" },
];

const roots = ["backend", "frontend", "scripts", "infra", ".github"].map((r) => join(repoRoot, r));
const generatedFolders = ["Migrations"];
const generatedPaths = ["/components/ui/"];
const selfPath = "scripts/checks/comment-policy.ts";

const findings: string[] = [];

for (const root of roots) {
  for (const file of walk(root, () => true)) {
    const repoPath = toRepoPath(file);
    const language = languages.find((l) => l.extensions.some((ext) => repoPath.endsWith(ext)));
    if (!language) continue;
    if (repoPath === selfPath) continue;
    if (generatedFolders.some((folder) => hasSegment(repoPath, folder)) || generatedPaths.some((path) => repoPath.includes(path))) continue;
    scan(repoPath, readFileSync(file, "utf8"), language);
  }
}

if (findings.length > 0) {
  console.error("Comment policy violations:");
  for (const finding of findings) console.error(`  ${finding}`);
  process.exit(1);
}

console.log("comment policy ok");

function scan(repoPath: string, content: string, language: Language): void {
  let inBlock = false;
  content.split(/\r?\n/).forEach((line, index) => {
    let commentText: string | undefined;
    if (inBlock) {
      commentText = line;
      if (language.blockEnd?.test(line)) inBlock = false;
    } else if (language.line.some((pattern) => pattern.test(line))) {
      commentText = line;
    } else if (language.blockStart?.test(line)) {
      commentText = line.slice(line.search(language.blockStart));
      inBlock = !language.blockEnd?.test(commentText);
    }
    if (commentText === undefined) return;
    for (const { pattern, reason } of bannedPatterns) {
      if (pattern.test(commentText)) findings.push(`${repoPath}:${index + 1}: ${reason} — ${commentText.trim()}`);
    }
  });
}
