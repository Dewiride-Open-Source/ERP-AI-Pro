export const severities = ["info", "low", "moderate", "high", "critical"] as const;

export type Severity = (typeof severities)[number];

export const blockingSeverities: readonly Severity[] = ["high", "critical"];

export interface ReviewedAdvisory {
  readonly id: string;
  readonly name: string;
  readonly through: readonly string[];
  readonly reason: string;
}

export const reviewedAdvisories: readonly ReviewedAdvisory[] = [];

export interface Advisory {
  readonly id: string;
  readonly name: string;
  readonly severity: Severity;
  readonly title: string;
  readonly url: string;
  readonly vulnerableVersions: string;
  readonly patchedVersions: string | undefined;
  readonly versions: readonly string[];
  readonly paths: readonly string[];
}

export interface AuditReport {
  readonly advisories: readonly Advisory[];
  readonly packageCount: number;
}

interface PnpmAuditFinding {
  readonly version: string;
  readonly paths: readonly string[];
}

interface PnpmAdvisory {
  readonly github_advisory_id?: string;
  readonly module_name: string;
  readonly severity: string;
  readonly title: string;
  readonly url: string;
  readonly vulnerable_versions: string;
  readonly patched_versions?: string | null;
  readonly findings: readonly PnpmAuditFinding[];
}

interface PnpmAuditReport {
  readonly advisories?: Record<string, PnpmAdvisory>;
  readonly metadata?: { readonly totalDependencies?: number };
}

function isSeverity(value: string): value is Severity {
  return (severities as readonly string[]).includes(value);
}

export function isBlocking(severity: Severity): boolean {
  return blockingSeverities.includes(severity);
}

export function pnpmAuditReport(report: string): AuditReport {
  const parsed = JSON.parse(report) as PnpmAuditReport | null;
  const advisories = parsed?.advisories;
  const packageCount = parsed?.metadata?.totalDependencies;
  if (typeof advisories !== "object" || advisories === null || typeof packageCount !== "number") {
    throw new Error("the report has no advisories and metadata.totalDependencies");
  }
  return {
    packageCount,
    advisories: Object.values(advisories).map((advisory) => {
      if (!isSeverity(advisory.severity)) throw new Error(`advisory ${advisory.url} has the unknown severity "${advisory.severity}"`);
      return {
        id: advisory.github_advisory_id || advisory.url,
        name: advisory.module_name,
        severity: advisory.severity,
        title: advisory.title,
        url: advisory.url,
        vulnerableVersions: advisory.vulnerable_versions,
        patchedVersions: advisory.patched_versions ?? undefined,
        versions: [...new Set(advisory.findings.map((finding) => finding.version))].sort(),
        paths: advisory.findings.flatMap((finding) => finding.paths).sort(),
      };
    }),
  };
}

export function directDependency(path: string): string {
  const segments = path.split(">");
  return segments[1] ?? segments[0] ?? path;
}

export function describeAdvisory(advisory: Advisory): string {
  return `${advisory.name} ${advisory.versions.join(", ")}: ${advisory.id} (${advisory.severity}) ${advisory.title}`;
}

function blockingFindings(advisory: Advisory, review: ReviewedAdvisory | undefined): string[] {
  const subject = describeAdvisory(advisory);
  if (review === undefined) {
    return [
      advisory.patchedVersions === undefined
        ? `${subject}; no fixed version is published, so remove the dependency or review it into reviewedAdvisories (${advisory.url})`
        : `${subject}; upgrade to ${advisory.patchedVersions} (${advisory.url})`,
    ];
  }
  const uncovered = advisory.paths.filter((path) => !review.through.includes(directDependency(path)));
  return [
    ...(advisory.patchedVersions === undefined ? [] : [`${subject}; ${advisory.patchedVersions} is published, so upgrade and remove its reviewedAdvisories entry`]),
    ...uncovered.map((path) => `${subject}; reached through ${path}, which its review does not name`),
  ];
}

export function advisoryFindings(advisories: readonly Advisory[], reviewed: readonly ReviewedAdvisory[] = reviewedAdvisories): string[] {
  const blocking = advisories.filter((advisory) => isBlocking(advisory.severity));
  const stale = reviewed.filter((review) => !blocking.some((advisory) => advisory.id === review.id));
  return [
    ...blocking.flatMap((advisory) => blockingFindings(advisory, reviewed.find((review) => review.id === advisory.id))),
    ...stale.map((review) => `${review.name} ${review.id}: no longer reported as high or critical, so remove its reviewedAdvisories entry`),
  ];
}
