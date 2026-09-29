import { createHash } from "node:crypto";

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

export type LicenceEvidence =
  | { readonly kind: "declared"; readonly licence: string }
  | { readonly kind: "file"; readonly sha256: string }
  | { readonly kind: "url"; readonly url: string };

export interface ReviewedPackage {
  readonly ecosystem: Ecosystem;
  readonly name: string;
  readonly evidence: LicenceEvidence;
  readonly reason: string;
}

export const reviewedPackages: readonly ReviewedPackage[] = [
  {
    ecosystem: "npm",
    name: "geist",
    evidence: { kind: "declared", licence: "SIL OPEN FONT LICENSE" },
    reason: "SIL Open Font License 1.1 (OFL-1.1), declared in package.json as the free text \"SIL OPEN FONT LICENSE\"; LICENSE.txt read",
  },
  {
    ecosystem: "nuget",
    name: "Microsoft.Data.SqlClient.SNI.runtime",
    evidence: { kind: "file", sha256: "9335e8bad875dd7be4eebd55d2335eb6433d1cea61aadb3817af7807bef8932a" },
    reason: "Microsoft Software License Terms for the SqlClient SNI native library, distributable as object code inside an application; LICENSE.txt read",
  },
  {
    ecosystem: "nuget",
    name: "Microsoft.Identity.Client.NativeInterop",
    evidence: { kind: "file", sha256: "0fe665c2ada5962fd01190ab5b2947b9a9d2c625a9f8225577441eb3256e14e3" },
    reason:
      "Microsoft Software License Terms for the MSAL native runtime that Microsoft.Data.SqlClient.Extensions.Azure brings in through Microsoft.Identity.Client.Broker; LICENSE read: section 3(e) forbids distributing it, so it runs only on Dewiride's own servers and first-deployment-release-and-deploy-pipeline keeps it out of every image that is conveyed",
  },
  {
    ecosystem: "nuget",
    name: "Microsoft.Testing.Extensions.CodeCoverage",
    evidence: { kind: "file", sha256: "b2a6b8b349a2b87d5b05ed687c4618f46052b90090352354b11bacb24f9bcb72" },
    reason: "Microsoft Software License Terms for a .NET library; test tooling that never ships; License.txt read",
  },
];

export interface PackageLicence {
  readonly ecosystem: Ecosystem;
  readonly name: string;
  readonly version: string;
  readonly licence: string;
  readonly expression: string | undefined;
  readonly evidence: LicenceEvidence | undefined;
}

export type NuspecLicence =
  | { readonly kind: "expression"; readonly expression: string }
  | { readonly kind: "file"; readonly file: string }
  | { readonly kind: "url"; readonly url: string }
  | { readonly kind: "missing" };

export interface RestoredPackage {
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

function reviewOf(entry: PackageLicence, reviewed: readonly ReviewedPackage[]): ReviewedPackage | undefined {
  return reviewed.find(
    (review) =>
      review.ecosystem === entry.ecosystem && (entry.ecosystem === "nuget" ? review.name.toLowerCase() === entry.name.toLowerCase() : review.name === entry.name),
  );
}

function evidenceValue(evidence: LicenceEvidence): string {
  switch (evidence.kind) {
    case "declared":
      return evidence.licence;
    case "file":
      return evidence.sha256;
    case "url":
      return evidence.url;
  }
}

function describeEvidence(evidence: LicenceEvidence): string {
  switch (evidence.kind) {
    case "declared":
      return `declared licence "${evidence.licence}"`;
    case "file":
      return `licence file SHA-256 ${evidence.sha256}`;
    case "url":
      return `licence URL ${evidence.url}`;
  }
}

export function isReviewed(entry: PackageLicence, reviewed: readonly ReviewedPackage[] = reviewedPackages): boolean {
  const pinned = reviewOf(entry, reviewed)?.evidence;
  return pinned !== undefined && entry.evidence?.kind === pinned.kind && evidenceValue(entry.evidence) === evidenceValue(pinned);
}

export function isWithinPolicy(entry: PackageLicence, reviewed: readonly ReviewedPackage[] = reviewedPackages): boolean {
  return (entry.expression !== undefined && satisfiesPolicy(entry.expression)) || isReviewed(entry, reviewed);
}

export function policyFinding(entry: PackageLicence, reviewed: readonly ReviewedPackage[] = reviewedPackages): string | undefined {
  if (isWithinPolicy(entry, reviewed)) return undefined;
  const review = reviewOf(entry, reviewed);
  return review === undefined
    ? entry.licence
    : `${entry.licence} (licence changed since it was reviewed as ${describeEvidence(review.evidence)}; read it again and update reviewedPackages)`;
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

export function nugetPackageLicence(name: string, version: string, licence: NuspecLicence, licenceFile?: Uint8Array): PackageLicence {
  switch (licence.kind) {
    case "expression":
      return {
        ecosystem: "nuget",
        name,
        version,
        licence: licence.expression,
        expression: licence.expression,
        evidence: { kind: "declared", licence: licence.expression },
      };
    case "file": {
      if (licenceFile === undefined) {
        return { ecosystem: "nuget", name, version, licence: `licence file ${licence.file}, missing from the package`, expression: undefined, evidence: undefined };
      }
      const sha256 = createHash("sha256").update(licenceFile).digest("hex");
      return { ecosystem: "nuget", name, version, licence: `licence file ${licence.file}, SHA-256 ${sha256}`, expression: undefined, evidence: { kind: "file", sha256 } };
    }
    case "url":
      return { ecosystem: "nuget", name, version, licence: `licence URL ${licence.url}`, expression: undefined, evidence: { kind: "url", url: licence.url } };
    case "missing":
      return { ecosystem: "nuget", name, version, licence: "no licence declared", expression: undefined, evidence: undefined };
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
      return entry.versions.map((version) => ({
        ecosystem: "npm" as const,
        name: entry.name,
        version,
        licence,
        expression: licence,
        evidence: { kind: "declared" as const, licence },
      }));
    }),
  );
}

interface AssetsLibrary {
  readonly type: string;
}

export function assetsFilePackages(assetsFile: string): RestoredPackage[] {
  const { libraries } = JSON.parse(assetsFile) as { libraries?: Record<string, AssetsLibrary> };
  return Object.entries(libraries ?? {}).flatMap(([key, library]) => {
    if (library.type !== "package") return [];
    const separator = key.indexOf("/");
    if (separator <= 0 || separator === key.length - 1) throw new Error(`project.assets.json library "${key}" is not <name>/<version>`);
    return [{ name: key.slice(0, separator), version: key.slice(separator + 1) }];
  });
}

interface ManifestTool {
  readonly version: string;
}

export function toolManifestPackages(manifest: string): RestoredPackage[] {
  const { tools } = JSON.parse(manifest) as { tools?: Record<string, ManifestTool> };
  return Object.entries(tools ?? {}).map(([name, tool]) => {
    if (typeof tool.version !== "string" || tool.version.length === 0) throw new Error(`dotnet-tools.json tool "${name}" has no version`);
    return { name, version: tool.version };
  });
}
