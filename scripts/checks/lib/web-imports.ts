import { posix } from "node:path";

import { byLocation, isCodeFile, stripCodeExtension, type WebRule, type WebSourceFile, type WebViolation } from "./web-source.ts";

type Place =
  | { readonly kind: "app" }
  | { readonly kind: "registry" }
  | { readonly kind: "domain-shared"; readonly domain: string }
  | { readonly kind: "module"; readonly domain: string; readonly module: string; readonly publicSurface: boolean }
  | { readonly kind: "features" }
  | { readonly kind: "shared" }
  | { readonly kind: "entry" };

type Problem = { readonly rule: WebRule; readonly message: string };

const staticImport = /(?:^|\n)\s*(?:import|export)\s[^;]*?\sfrom\s+["']([^"']+)["']/g;
const sideEffectImport = /(?:^|\n)\s*import\s+["']([^"']+)["']/g;
const dynamicImport = /\bimport\s*\(\s*["']([^"']+)["']\s*\)/g;

export function importViolations(files: readonly WebSourceFile[]): WebViolation[] {
  const found: WebViolation[] = [];
  for (const file of files) {
    if (!isCodeFile(file.path)) continue;
    for (const specifier of new Set(specifiers(file.content))) {
      const problem = violation(file.path, specifier);
      if (problem) found.push({ path: file.path, rule: problem.rule, message: `import "${specifier}" — ${problem.message}` });
    }
  }
  return found.sort(byLocation);
}

function specifiers(content: string): string[] {
  return [staticImport, sideEffectImport, dynamicImport].flatMap((pattern) =>
    [...content.matchAll(pattern)].map((match) => match[1]).filter((value): value is string => Boolean(value)),
  );
}

function isRelative(specifier: string): boolean {
  return specifier === "." || specifier === ".." || specifier.startsWith("./") || specifier.startsWith("../");
}

function resolveTarget(source: string, specifier: string): string | undefined {
  let target: string;
  if (specifier.startsWith("@/")) target = posix.normalize(specifier.slice(2));
  else if (isRelative(specifier)) target = posix.normalize(posix.join(posix.dirname(source), specifier));
  else return undefined;
  if (target === ".." || target.startsWith("../")) return undefined;
  return stripCodeExtension(target.replace(/\/$/, ""));
}

function placeOf(path: string): Place {
  const segments = path.toLowerCase().split("/");
  const [top, domain, module, next] = segments;
  if (top === "app") return { kind: "app" };
  if (top === "shared") return { kind: "shared" };
  if (top !== "features") return { kind: "entry" };
  if (domain === "registry" && segments.length === 2) return { kind: "registry" };
  if (domain === undefined || module === undefined) return { kind: "features" };
  if (module === "_shared") return { kind: "domain-shared", domain };
  const publicSurface = segments.length === 3 || (segments.length === 4 && next === "index");
  return { kind: "module", domain, module, publicSurface };
}

function violation(source: string, specifier: string): Problem | undefined {
  const target = resolveTarget(source, specifier);
  if (target === undefined) return undefined;
  const from = placeOf(stripCodeExtension(source));
  const to = placeOf(target);

  if (from.kind === "app") {
    if (!specifier.startsWith("@/")) {
      return { rule: "I3", message: "app/ imports through the @/ alias only, never a relative path" };
    }
    if (to.kind === "shared" || to.kind === "registry" || (to.kind === "module" && to.publicSurface)) return undefined;
    return {
      rule: "I3",
      message: "app/ imports only a module's public surface (@/features/<domain>/<module>), @/features/registry or @/shared/**",
    };
  }
  if (to.kind === "app") {
    return { rule: "I6", message: "nothing outside app/ imports app/**" };
  }
  if ((from.kind === "shared" || from.kind === "entry") && to.kind !== "shared" && to.kind !== "entry") {
    const message =
      from.kind === "shared"
        ? "shared/ never imports features/; the route in app/ passes feature data in as props"
        : `${source} never imports features/`;
    return { rule: "I5", message };
  }
  if (to.kind === "registry") {
    return { rule: "I4", message: "features/registry.ts is imported only from app/" };
  }
  if (to.kind === "domain-shared") {
    const sameDomain = (from.kind === "module" || from.kind === "domain-shared") && from.domain === to.domain;
    return sameDomain
      ? undefined
      : { rule: "I2", message: `features/${to.domain}/_shared is importable only from features/${to.domain}/**` };
  }
  if (to.kind === "module" && !to.publicSurface) {
    const sameModule = from.kind === "module" && from.domain === to.domain && from.module === to.module;
    if (!sameModule) {
      return {
        rule: "I1",
        message: `reaches into features/${to.domain}/${to.module} internals; import its public surface @/features/${to.domain}/${to.module}`,
      };
    }
  }
  return undefined;
}
