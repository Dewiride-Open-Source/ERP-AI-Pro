import { readFileSync } from "node:fs";
import { dirname, join, posix } from "node:path";

import { repoRoot, toRepoPath, walk } from "./lib/walk.ts";

const webSource = join(repoRoot, "frontend", "apps", "web", "src");
const staticImport = /(?:^|\n)\s*(?:import|export)\s[^;]*?\sfrom\s+["']([^"']+)["']/g;
const sideEffectImport = /(?:^|\n)\s*import\s+["']([^"']+)["']/g;
const dynamicImport = /\bimport\s*\(\s*["']([^"']+)["']\s*\)/g;

const findings: string[] = [];

for (const file of walk(webSource, (f) => f.endsWith(".ts") || f.endsWith(".tsx"))) {
  const repoPath = toRepoPath(file);
  const source = repoPath.replace("frontend/apps/web/src/", "");
  const content = readFileSync(file, "utf8");
  for (const specifier of specifiers(content)) {
    const target = resolveTarget(source, specifier);
    if (!target) continue;
    const problem = violation(source, target);
    if (problem) findings.push(`${repoPath}: import "${specifier}" — ${problem}`);
  }
}

if (findings.length > 0) {
  console.error("Feature boundary violations:");
  for (const finding of findings) console.error(`  ${finding}`);
  process.exit(1);
}

console.log("feature boundaries ok");

function specifiers(content: string): string[] {
  return [staticImport, sideEffectImport, dynamicImport].flatMap((pattern) =>
    [...content.matchAll(pattern)].map((match) => match[1]).filter((value): value is string => Boolean(value)),
  );
}

function resolveTarget(source: string, specifier: string): string | undefined {
  if (specifier.startsWith("@/")) return specifier.slice(2);
  if (specifier.startsWith(".")) return posix.normalize(posix.join(dirname(source), specifier));
  return undefined;
}

function moduleOf(path: string): string | undefined {
  const match = /^features\/([^/]+)\/([^/]+)(?:\/(.*))?$/.exec(path);
  return match ? `${match[1]}/${match[2]}` : undefined;
}

function domainOf(path: string): string | undefined {
  return /^features\/([^/]+)\//.exec(path)?.[1];
}

function violation(source: string, target: string): string | undefined {
  const targetModule = moduleOf(target);
  if (!targetModule) return undefined;
  if (targetModule.endsWith("/_shared")) {
    const domain = domainOf(target);
    return domainOf(source) === domain
      ? undefined
      : `features/${domain}/_shared is importable only from features/${domain}/**`;
  }
  const isPublicSurface = target === `features/${targetModule}` || target === `features/${targetModule}/index`;
  const sourceModule = moduleOf(source);

  if (source.startsWith("app/")) {
    return isPublicSurface ? undefined : `routes may import a module only through features/${targetModule}/index.ts`;
  }
  if (sourceModule === targetModule) return undefined;
  if (source === "features/registry.ts") {
    return isPublicSurface ? undefined : "the registry imports only module public surfaces";
  }
  return isPublicSurface ? undefined : `features/${sourceModule ?? source} reaches into ${targetModule} internals`;
}
