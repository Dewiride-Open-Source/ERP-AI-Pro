import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { parseArgs } from "node:util";

import {
  claFolder,
  coAuthorTrailerFormat,
  commitIdentity,
  type Exemptions,
  exemptionsPath,
  isSignaturePath,
  latestCalendarDate,
  parseNameStatus,
  pullRequestAuthorProblem,
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

interface TreeEntry {
  readonly mode: string;
  readonly type: string;
  readonly object: string;
  readonly path: string;
}

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

function signatureTree(commit: string): TreeEntry[] {
  return git("ls-tree", "-r", "-z", commit, "--", ...signatureFolders)
    .split("\0")
    .filter((entry) => entry !== "")
    .map((entry) => {
      const tab = entry.indexOf("\t");
      const [mode = "", type = "", object = ""] = entry.slice(0, tab).split(" ");
      return { mode, type, object, path: entry.slice(tab + 1) };
    });
}

function isRegularFile(entry: TreeEntry): boolean {
  return entry.type === "blob" && regularFileModes.includes(entry.mode);
}

function readBlob(entry: TreeEntry): SignatureFile {
  return { path: entry.path, text: git("cat-file", "blob", entry.object) };
}

function checkPullRequest(baseReference: string, headReference: string, prAuthor: string): void {
  if (!pullRequestAuthor.test(prAuthor)) fail("Usage error:", [`${prAuthor} is not a GitHub login`]);
  const baseCommit = resolveCommit(baseReference);
  const headCommit = resolveCommit(headReference);

  const problems: string[] = [];
  const changes = parseNameStatus(
    git("diff", "--name-status", "--no-renames", "--no-textconv", "--no-ext-diff", "-z", `${baseCommit}...${headCommit}`, "--", claFolder),
  );
  const changed = new Set(changes.map((change) => change.path));
  const changedInHead = signatureTree(headCommit).filter((entry) => changed.has(entry.path));
  for (const entry of changedInHead) {
    if (!isRegularFile(entry)) problems.push(`${entry.path}: a signature is a regular file`);
  }
  const baseTree = signatureTree(baseCommit).filter(isRegularFile);
  const baseFiles = baseTree.filter((entry) => changed.has(entry.path)).map(readBlob);
  const files = [
    ...baseTree.filter((entry) => !changed.has(entry.path)).map(readBlob),
    ...changedInHead.filter(isRegularFile).map(readBlob),
  ];
  const { signatures, problems: signatureProblems } = readSignatures(files, today, exemptions);
  problems.push(...signatureProblems, ...signatureChangeProblems(changes, prAuthor, exemptions, { base: baseFiles, head: files }));

  const commits = git("rev-list", `${baseCommit}..${headCommit}`)
    .split("\n")
    .filter((sha) => sha !== "")
    .map((sha) =>
      commitIdentity(
        sha,
        git("-c", "log.showSignature=false", "log", "-1", "--format=%ae", sha),
        git("-c", "log.showSignature=false", "log", "-1", `--format=${coAuthorTrailerFormat}`, sha),
      ),
    );
  const uncovered = uncoveredCommits(commits, signatures, exemptions, prAuthor);
  const authorProblem = pullRequestAuthorProblem(prAuthor, signatures, exemptions);

  if (problems.length === 0 && uncovered.length === 0 && authorProblem === undefined) {
    console.log(`cla ok: ${prAuthor} and every author and co-author of ${commits.length} commits are exempt or signed`);
    return;
  }
  if (authorProblem !== undefined) console.error(`${authorProblem}.`);
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
