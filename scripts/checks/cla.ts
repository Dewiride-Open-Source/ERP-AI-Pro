import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { parseArgs } from "node:util";

import {
  claFolder,
  commitLogFormat,
  type Exemptions,
  exemptionsPath,
  isSignaturePath,
  latestCalendarDate,
  parseCommitLog,
  parseNameStatus,
  readExemptions,
  readSignatures,
  type Signature,
  signatureChangeProblems,
  signatureFolders,
  type SignatureFile,
  type SignatureKind,
  uncoveredCommits,
} from "./lib/cla.ts";
import { repoRoot } from "./lib/walk.ts";

const signingGuide = "docs/cla/sign-cla.md (https://github.com/Dewiride-Open-Source/ERP-AI-Pro/blob/main/docs/cla/sign-cla.md)";
const commitReference = /^[A-Za-z0-9][A-Za-z0-9._/~^-]*$/;
const pullRequestAuthor = /^[A-Za-z0-9][A-Za-z0-9-]*(?:\[bot\])?$/;
const regularFileModes = ["100644", "100755"];

const { values } = parseArgs({
  options: {
    base: { type: "string" },
    head: { type: "string" },
    "pull-request-author": { type: "string" },
    root: { type: "string" },
    help: { type: "boolean", default: false },
  },
});

if (values.help) {
  console.log("Usage: node scripts/checks/cla.ts [--base <commit> --head <commit> --pull-request-author <login>] [--root <repository>]");
  process.exit(0);
}

const root = resolve(values.root ?? repoRoot);
const git = (...args: string[]): string => execFileSync("git", ["-C", root, ...args], { encoding: "utf8", maxBuffer: 64 * 1024 * 1024 });
const today = latestCalendarDate(new Date());
const exemptions = loadExemptions();
const { base, head } = values;
const author = values["pull-request-author"];

if (base === undefined && head === undefined && author === undefined) checkWorkingTree();
else if (base !== undefined && head !== undefined && author !== undefined) checkPullRequest(base, head, author);
else fail("Usage error:", ["--base, --head and --pull-request-author are given together, or none of them"]);

function fail(heading: string, lines: readonly string[]): never {
  console.error(heading);
  for (const line of lines) console.error(`  ${line}`);
  process.exit(1);
}

function count(signatures: readonly Signature[], kind: SignatureKind): number {
  return signatures.filter((signature) => signature.kind === kind).length;
}

function loadExemptions(): Exemptions {
  try {
    return readExemptions(readFileSync(join(root, exemptionsPath), "utf8"));
  } catch (error) {
    return fail("The contributor licence agreement exemptions cannot be read:", [`${exemptionsPath}: ${(error as Error).message}`]);
  }
}

function resolveCommit(reference: string): string {
  if (commitReference.test(reference)) {
    try {
      return git("rev-parse", "--verify", "--quiet", `${reference}^{commit}`).trim();
    } catch {
      return fail("Usage error:", [`${reference} is not a commit in ${root}`]);
    }
  }
  return fail("Usage error:", [`${reference} is not a commit reference`]);
}

function checkWorkingTree(): void {
  const paths = new Set(
    git("ls-files", "-z", "--cached", "--others", "--exclude-standard", "--", claFolder)
      .split("\0")
      .filter((path) => isSignaturePath(path)),
  );
  const files: SignatureFile[] = [];
  for (const path of paths) {
    try {
      files.push({ path, text: readFileSync(join(root, path), "utf8") });
    } catch {
      continue;
    }
  }
  const { signatures, problems } = readSignatures(files, today, exemptions);
  if (problems.length > 0) fail(`Malformed contributor licence agreement signatures (see ${signingGuide}):`, problems);
  console.log(`cla ok: ${count(signatures, "individual")} individual and ${count(signatures, "corporate")} corporate signatures`);
}

function checkPullRequest(baseReference: string, headReference: string, prAuthor: string): void {
  if (!pullRequestAuthor.test(prAuthor)) fail("Usage error:", [`${prAuthor} is not a GitHub login`]);
  const baseCommit = resolveCommit(baseReference);
  const headCommit = resolveCommit(headReference);

  const problems: string[] = [];
  const files: SignatureFile[] = [];
  const entries = git("ls-tree", "-r", "-z", headCommit, "--", ...signatureFolders)
    .split("\0")
    .filter((entry) => entry !== "");
  for (const entry of entries) {
    const tab = entry.indexOf("\t");
    const [mode = "", type = "", object = ""] = entry.slice(0, tab).split(" ");
    const path = entry.slice(tab + 1);
    if (type !== "blob" || !regularFileModes.includes(mode)) {
      problems.push(`${path}: a signature is a regular file`);
      continue;
    }
    files.push({ path, text: git("cat-file", "blob", object) });
  }
  const { signatures, problems: signatureProblems } = readSignatures(files, today, exemptions);
  problems.push(...signatureProblems);

  const changes = parseNameStatus(git("diff", "--name-status", "--no-renames", "-z", `${baseCommit}...${headCommit}`, "--", claFolder));
  problems.push(...signatureChangeProblems(changes, prAuthor, exemptions));

  const commits = parseCommitLog(
    git("-c", "log.showSignature=false", "log", `--format=${commitLogFormat}`, `${baseCommit}..${headCommit}`),
  );
  const uncovered = uncoveredCommits(commits, signatures, exemptions, prAuthor);

  if (problems.length === 0 && uncovered.length === 0) {
    console.log(`cla ok: every author and co-author of ${commits.length} commits is exempt or signed`);
    return;
  }
  if (problems.length > 0) {
    console.error("Contributor licence agreement signatures that cannot be accepted:");
    for (const problem of problems) console.error(`  ${problem}`);
  }
  if (uncovered.length > 0) {
    console.error("Commits whose author or co-author has not signed the contributor licence agreement:");
    for (const { sha, email, role } of uncovered) console.error(`  ${sha.slice(0, 7)} ${email} (${role})`);
  }
  console.error(`Sign the agreement or correct the signature as ${signingGuide} describes, then push again.`);
  process.exit(1);
}
