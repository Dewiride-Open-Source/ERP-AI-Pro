import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { parseArgs } from "node:util";

import { advisoryFindings, type AuditReport, describeAdvisory, isBlocking, pnpmAuditReport, reviewedAdvisories } from "./lib/advisory-policy.ts";
import { repoRoot } from "./lib/walk.ts";

const usage = `Usage: node scripts/checks/advisories.ts [options]

  Audits every package of the frontend workspace's lockfile (pnpm audit --json) and fails on a high or critical advisory
  that scripts/checks/lib/advisory-policy.ts has not reviewed, and on a reviewed one that a published version fixes, that
  is reached through a dependency its review does not name, or that is no longer reported.

Options: --root <dir> (repository root), --from <file> (a saved pnpm audit --json report).`;

const { positionals, values } = parseArgs({
  allowPositionals: true,
  options: {
    root: { type: "string" },
    from: { type: "string" },
    help: { type: "boolean", default: false },
  },
});

if (values.help || positionals.length > 0) {
  console.log(usage);
  process.exit(values.help ? 0 : 64);
}

const root = values.root === undefined ? repoRoot : resolve(values.root);

function fail(message: string): never {
  console.error(message);
  process.exit(1);
}

function auditOutput(): { stdout: string; stderr: string } {
  if (values.from !== undefined) return { stdout: readFileSync(resolve(values.from), "utf8"), stderr: "" };
  const result = spawnSync("pnpm audit --json", { cwd: join(root, "frontend"), encoding: "utf8", maxBuffer: 64 * 1024 * 1024, shell: true });
  if (result.error !== undefined) fail(`pnpm audit could not start: ${result.error.message}`);
  return { stdout: result.stdout ?? "", stderr: result.stderr ?? "" };
}

function auditReport(): AuditReport {
  const { stdout, stderr } = auditOutput();
  try {
    return pnpmAuditReport(stdout);
  } catch (error) {
    return fail(`pnpm audit --json printed no report (${error instanceof Error ? error.message : String(error)})\n${stderr}`);
  }
}

const report = auditReport();
for (const advisory of report.advisories.filter((entry) => !isBlocking(entry.severity))) console.log(`  below high, not blocking: ${describeAdvisory(advisory)}`);

const findings = advisoryFindings(report.advisories);
if (findings.length > 0) {
  console.error("npm advisories outside the advisory policy (scripts/checks/lib/advisory-policy.ts):");
  for (const finding of findings) console.error(`  ${finding}`);
  process.exit(1);
}

const unfixed = reviewedAdvisories.length;
console.log(`advisories ok: ${report.packageCount} npm packages audited, ${unfixed} reviewed ${unfixed === 1 ? "advisory" : "advisories"} without a published fix`);
