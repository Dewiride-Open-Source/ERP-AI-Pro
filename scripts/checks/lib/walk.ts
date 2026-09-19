import { readdirSync, statSync } from "node:fs";
import { dirname, join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

export const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");

const ignoredDirectories = new Set([
  ".git",
  ".next",
  "node_modules",
  "artifacts",
  "bin",
  "obj",
  "test-results",
  "playwright-report",
  "screenshots",
  ".pnpm-store",
]);

export function walk(root: string, accept: (file: string) => boolean): string[] {
  const files: string[] = [];
  const visit = (directory: string) => {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      if (entry.isDirectory()) {
        if (!ignoredDirectories.has(entry.name)) visit(join(directory, entry.name));
      } else if (entry.isFile()) {
        const file = join(directory, entry.name);
        if (accept(file)) files.push(file);
      }
    }
  };
  if (statSync(root).isDirectory()) visit(root);
  return files;
}

export function toRepoPath(file: string): string {
  return relative(repoRoot, file).split(sep).join("/");
}

export function hasSegment(repoPath: string, segment: string): boolean {
  return repoPath.split("/").includes(segment);
}
