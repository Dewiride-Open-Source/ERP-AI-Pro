import { execFileSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";

import { repoRoot } from "./walk.ts";

export type WebRule = "S1" | "S2" | "S3" | "S4" | "S5" | "S6" | "I1" | "I2" | "I3" | "I4" | "I5" | "I6" | "I7";

export interface WebSourceFile {
  readonly path: string;
  readonly content: string;
}

export interface WebViolation {
  readonly path: string;
  readonly rule: WebRule;
  readonly message: string;
}

export const webSourceRoot = join(repoRoot, "frontend", "apps", "web", "src");

const codeFile = /\.(?:[cm]?[jt]s|[jt]sx)$/;

export function isCodeFile(path: string): boolean {
  return codeFile.test(path);
}

export function stripCodeExtension(path: string): string {
  return path.replace(codeFile, "");
}

export function readWebSource(root: string = webSourceRoot): WebSourceFile[] {
  const listed = execFileSync("git", ["-C", root, "ls-files", "-z", "--cached", "--others", "--exclude-standard", "--", "."], {
    encoding: "utf8",
    maxBuffer: 64 * 1024 * 1024,
  });
  const paths = [...new Set(listed.split("\0"))].filter((path) => path !== "" && existsSync(join(root, path)));
  return paths.map((path) => ({ path, content: isCodeFile(path) ? readFileSync(join(root, path), "utf8") : "" }));
}

export function byLocation(a: WebViolation, b: WebViolation): number {
  return a.path.localeCompare(b.path) || a.rule.localeCompare(b.rule) || a.message.localeCompare(b.message);
}
