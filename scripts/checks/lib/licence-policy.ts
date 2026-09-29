export const allowedLicences = [
  "0BSD",
  "Apache-2.0",
  "BlueOak-1.0.0",
  "BSD-2-Clause",
  "BSD-3-Clause",
  "CC-BY-4.0",
  "CC0-1.0",
  "ISC",
  "LGPL-2.1-only",
  "LGPL-2.1-or-later",
  "LGPL-3.0-only",
  "LGPL-3.0-or-later",
  "MIT",
  "MIT-0",
  "MPL-2.0",
  "OFL-1.1",
  "Python-2.0",
  "Unlicense",
  "Zlib",
] as const;

export type Ecosystem = "npm" | "nuget";

export interface ReviewedPackage {
  readonly ecosystem: Ecosystem;
  readonly name: string;
  readonly reason: string;
}

export const reviewedPackages: readonly ReviewedPackage[] = [
  {
    ecosystem: "npm",
    name: "geist",
    reason: "SIL Open Font License 1.1 (OFL-1.1), declared in package.json as the free text \"SIL OPEN FONT LICENSE\"; LICENSE.txt read",
  },
  {
    ecosystem: "nuget",
    name: "Microsoft.Data.SqlClient.SNI.runtime",
    reason: "Microsoft Software License Terms for the SqlClient SNI native library, distributable as object code inside an application; LICENSE.txt read",
  },
  {
    ecosystem: "nuget",
    name: "Microsoft.Identity.Client.NativeInterop",
    reason:
      "Microsoft Software License Terms for the MSAL native runtime that Microsoft.Data.SqlClient.Extensions.Azure brings in through Microsoft.Identity.Client.Broker; LICENSE read: section 3(e) forbids distributing it, so it runs only on Dewiride's own servers and first-deployment-release-and-deploy-pipeline keeps it out of every image that is conveyed",
  },
  {
    ecosystem: "nuget",
    name: "Microsoft.Testing.Extensions.CodeCoverage",
    reason: "Microsoft Software License Terms for a .NET library; test tooling that never ships; License.txt read",
  },
];

export interface PackageLicence {
  readonly ecosystem: Ecosystem;
  readonly name: string;
  readonly version: string;
  readonly licence: string;
  readonly expression: string | undefined;
}

export type NuspecLicence =
  | { readonly kind: "expression"; readonly expression: string }
  | { readonly kind: "file"; readonly file: string }
  | { readonly kind: "url"; readonly url: string }
  | { readonly kind: "missing" };

export interface LockedPackage {
  readonly name: string;
  readonly version: string;
}

const allowedKeys = new Set<string>(allowedLicences.map((licence) => licence.toLowerCase()));
const operators = new Set(["AND", "OR", "WITH", "and", "or", "with"]);
const identifier = /^[A-Za-z0-9.-]+\+?$/;

function isIdentifier(token: string | undefined): token is string {
  return token !== undefined && identifier.test(token) && !operators.has(token);
}

function isAllowedLicence(licence: string): boolean {
  const key = licence.toLowerCase();
  if (!key.endsWith("+")) return allowedKeys.has(key) || allowedKeys.has(`${key}-only`);
  const base = key.slice(0, -1);
  return [base, `${base}-only`, `${base}-or-later`].some((candidate) => allowedKeys.has(candidate));
}

function evaluate(expression: string): boolean | undefined {
  const tokens = expression.match(/[()]|[^\s()]+/g) ?? [];
  let position = 0;

  const operator = (name: string): boolean => {
    const token = tokens[position];
    if (token !== name && token !== name.toLowerCase()) return false;
    position += 1;
    return true;
  };

  const term = (): boolean | undefined => {
    const token = tokens[position];
    if (token === "(") {
      position += 1;
      const inner = disjunction();
      if (inner === undefined || tokens[position] !== ")") return undefined;
      position += 1;
      return inner;
    }
    if (!isIdentifier(token)) return undefined;
    position += 1;
    if (!operator("WITH")) return isAllowedLicence(token);
    const exception = tokens[position];
    if (!isIdentifier(exception)) return undefined;
    position += 1;
    return allowedKeys.has(`${token} WITH ${exception}`.toLowerCase());
  };

  const conjunction = (): boolean | undefined => {
    let result = term();
    while (result !== undefined && operator("AND")) {
      const next = term();
      result = next === undefined ? undefined : result && next;
    }
    return result;
  };

  const disjunction = (): boolean | undefined => {
    let result = conjunction();
    while (result !== undefined && operator("OR")) {
      const next = conjunction();
      result = next === undefined ? undefined : result || next;
    }
    return result;
  };

  const result = disjunction();
  return position === tokens.length ? result : undefined;
}

export function satisfiesPolicy(expression: string): boolean {
  return evaluate(expression) === true;
}

export function isReviewed(ecosystem: Ecosystem, name: string): boolean {
  return reviewedPackages.some(
    (reviewed) => reviewed.ecosystem === ecosystem && (ecosystem === "nuget" ? reviewed.name.toLowerCase() === name.toLowerCase() : reviewed.name === name),
  );
}

export function isWithinPolicy(entry: PackageLicence): boolean {
  return isReviewed(entry.ecosystem, entry.name) || (entry.expression !== undefined && satisfiesPolicy(entry.expression));
}

function xmlText(value: string): string {
  return value
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&amp;/g, "&")
    .trim();
}

function decodedPath(value: string): string | undefined {
  try {
    return decodeURIComponent(value);
  } catch {
    return undefined;
  }
}

export function nuspecLicence(xml: string): NuspecLicence {
  const licence = /<license\b([^>]*)>([^<]*)<\/license>/.exec(xml);
  if (licence) {
    const type = /\btype\s*=\s*(["'])(.*?)\1/.exec(licence[1] ?? "")?.[2];
    const value = xmlText(licence[2] ?? "");
    if (type === "expression") return { kind: "expression", expression: value };
    if (type === "file") return { kind: "file", file: value };
  }
  const licenceUrl = /<licenseUrl>([^<]*)<\/licenseUrl>/.exec(xml);
  if (!licenceUrl) return { kind: "missing" };
  const url = xmlText(licenceUrl[1] ?? "");
  const path = /^https:\/\/licenses\.nuget\.org\/([^?#]+)$/.exec(url)?.[1];
  const expression = path === undefined ? undefined : decodedPath(path);
  return expression === undefined ? { kind: "url", url } : { kind: "expression", expression };
}

export function nugetPackageLicence(name: string, version: string, licence: NuspecLicence): PackageLicence {
  switch (licence.kind) {
    case "expression":
      return { ecosystem: "nuget", name, version, licence: licence.expression, expression: licence.expression };
    case "file":
      return { ecosystem: "nuget", name, version, licence: `licence file ${licence.file}`, expression: undefined };
    case "url":
      return { ecosystem: "nuget", name, version, licence: `licence URL ${licence.url}`, expression: undefined };
    case "missing":
      return { ecosystem: "nuget", name, version, licence: "no licence declared", expression: undefined };
  }
}

interface PnpmLicensedPackage {
  readonly name: string;
  readonly versions: readonly string[];
  readonly license?: string;
}

export function pnpmPackageLicences(listing: string): PackageLicence[] {
  const groups = JSON.parse(listing) as Record<string, readonly PnpmLicensedPackage[]>;
  return Object.entries(groups).flatMap(([group, packages]) =>
    packages.flatMap((entry) => {
      const licence = entry.license ?? group;
      return entry.versions.map((version) => ({ ecosystem: "npm" as const, name: entry.name, version, licence, expression: licence }));
    }),
  );
}

interface LockFileDependency {
  readonly type: string;
  readonly resolved?: string;
}

export function lockFilePackages(lockFile: string): LockedPackage[] {
  const { dependencies } = JSON.parse(lockFile) as { dependencies?: Record<string, Record<string, LockFileDependency>> };
  return Object.values(dependencies ?? {}).flatMap((target) =>
    Object.entries(target).flatMap(([name, dependency]) =>
      dependency.type === "Project" || dependency.resolved === undefined ? [] : [{ name, version: dependency.resolved }],
    ),
  );
}
