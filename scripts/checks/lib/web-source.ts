import { readFileSync } from "node:fs";
import { join, relative, sep } from "node:path";

import { repoRoot, walk } from "./walk.ts";

export type WebRule = "S1" | "S2" | "S3" | "S4" | "S5" | "S6" | "I1" | "I2" | "I3" | "I4" | "I5" | "I6";

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
  return walk(root, () => true).map((file) => {
    const path = relative(root, file).split(sep).join("/");
    return { path, content: isCodeFile(path) ? readFileSync(file, "utf8") : "" };
  });
}

export function byLocation(a: WebViolation, b: WebViolation): number {
  return a.path.localeCompare(b.path) || a.rule.localeCompare(b.rule) || a.message.localeCompare(b.message);
}
