export type SignatureKind = "individual" | "corporate";

export interface SignatureFile {
  readonly path: string;
  readonly text: string;
}

export interface Signatory {
  readonly email: string;
  readonly login: string;
}

interface SignatureCoverage {
  readonly path: string;
  readonly emails: readonly string[];
  readonly logins: readonly string[];
}

export interface IndividualSignature extends SignatureCoverage {
  readonly kind: "individual";
}

export interface CorporateSignature extends SignatureCoverage {
  readonly kind: "corporate";
  readonly signatory: Signatory;
}

export type Signature = IndividualSignature | CorporateSignature;

export type ParsedSignature = { readonly signature: Signature } | { readonly problems: readonly string[] };

export interface SignatureCheck {
  readonly signatures: readonly Signature[];
  readonly problems: readonly string[];
}

export interface Exemption {
  readonly login: string;
  readonly reason: string;
  readonly emails: readonly string[];
}

export type Exemptions = ReadonlyMap<string, Exemption>;

export interface Change {
  readonly status: string;
  readonly path: string;
}

export interface SignatureVersions {
  readonly base: readonly SignatureFile[];
  readonly head: readonly SignatureFile[];
}

export interface Commit {
  readonly sha: string;
  readonly author: string;
  readonly coAuthors: readonly string[];
}

export type CommitRole = "author" | "co-author";

export interface UncoveredEmail {
  readonly sha: string;
  readonly email: string;
  readonly role: CommitRole;
}

export const claFolder = "docs/cla";
export const exemptionsPath = "docs/cla/exempt.json";
export const signatureFolders = ["docs/cla/individual", "docs/cla/corporate"] as const;
export const commitLogFormat = "%H%x1f%ae%x1f%(trailers:key=Co-authored-by,valueonly,separator=%x1e)%x1d";

export const individualStatement = "I hereby agree to the terms of the ERP-AI-Pro Individual Contributor Licence Agreement v1.0.";
export const individualDeclaration = "I declare that I am authorised and able to make this agreement and sign this declaration.";
export const corporateDeclaration =
  "I declare that I am authorised and able to make this agreement and sign this declaration on behalf of the company named above.";
export const signedLine = "Signed,";
export const contributorsLine = "List of contributors:";

const corporateStatement = /^(?<company>\S.*?) agrees to the terms of the ERP-AI-Pro Corporate Contributor Licence Agreement v1\.0\.$/;
const personShape = "<name> <email> https://github.com/<login>";
const namingRule = "a signature is docs/cla/individual/<lowercase-github-login>.md or docs/cla/corporate/<company-slug>.md";

const folderKinds: ReadonlyArray<readonly [string, SignatureKind]> = [
  ["docs/cla/individual/", "individual"],
  ["docs/cla/corporate/", "corporate"],
];

const loginPattern = /^[a-z0-9](?:[a-z0-9]|-(?=[a-z0-9])){0,38}$/;
const exemptLoginPattern = /^[a-z0-9](?:[a-z0-9]|-(?=[a-z0-9])){0,38}(?:\[bot\])?$/i;
const botLogin = /\[bot\]$/i;
const companySlugPattern = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
const datedPlacePattern = /^(?<country>\p{L}[\p{L}\p{M} .'()-]*), (?<date>\d{4}-\d{2}-\d{2})$/u;
const personPattern = /^(?<name>\S(?:.*\S)?)\s+(?<email>\S+)\s+https:\/\/github\.com\/(?<login>\S+)$/;
const emailPattern =
  /^[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)+$/;
const exemptEmailPattern = /^[^\s@<>]+@[^\s@<>]+\.[^\s@<>]+$/;
const noReplyEmailPattern = /^(?:\d+\+)?([^@]+)@users\.noreply\.github\.com$/i;
const trailerIdentityPattern = /^[^<>]*<([^<>\s]+)>\s*$/;
const furthestAheadUtcOffsetMilliseconds = 14 * 60 * 60 * 1000;

interface Line {
  readonly number: number;
  readonly text: string;
}

interface Person {
  readonly line: Line;
  readonly email: string;
  readonly login: string;
}

type Report = (line: Line | undefined, message: string) => void;

export function latestCalendarDate(now: Date): string {
  return new Date(now.getTime() + furthestAheadUtcOffsetMilliseconds).toISOString().slice(0, 10);
}

export function isSignaturePath(path: string): boolean {
  return folderKinds.some(([folder]) => path.startsWith(folder));
}

function signatureLocation(path: string): { readonly kind: SignatureKind; readonly slug: string } | undefined {
  for (const [folder, kind] of folderKinds) {
    if (!path.startsWith(folder)) continue;
    const name = path.slice(folder.length);
    const slug = name.endsWith(".md") ? name.slice(0, -".md".length) : "";
    const pattern = kind === "individual" ? loginPattern : companySlugPattern;
    return pattern.test(slug) ? { kind, slug } : undefined;
  }
  return undefined;
}

function meaningfulLines(text: string): Line[] {
  return text
    .replace(/^\uFEFF/, "")
    .replace(/\r\n?/g, "\n")
    .split("\n")
    .map((content, index) => ({ number: index + 1, text: content.trim() }))
    .filter((line) => line.text !== "");
}

function isCalendarDate(value: string): boolean {
  const [year = 0, month = 0, day = 0] = value.split("-").map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day;
}

function checkDatedPlace(line: Line, today: string, report: Report): void {
  const date = datedPlacePattern.exec(line.text)?.groups?.date;
  if (date === undefined) report(line, 'expected "<country>, <YYYY-MM-DD>"');
  else if (!isCalendarDate(date)) report(line, `${date} is not a calendar date`);
  else if (date > today) report(line, `${date} is later than today (${today})`);
}

function readPerson(line: Line, report: Report): Person | undefined {
  const groups = personPattern.exec(line.text)?.groups;
  if (groups === undefined) {
    report(line, `expected "${personShape}"`);
    return undefined;
  }
  const email = groups.email ?? "";
  const login = groups.login ?? "";
  let valid = true;
  if (botLogin.test(login)) {
    report(line, `${login} is a bot account, and a bot never signs`);
    valid = false;
  } else if (!loginPattern.test(login.toLowerCase())) {
    report(line, `${login} is not a GitHub login`);
    valid = false;
  }
  const noReplyLogin = noReplyEmailPattern.exec(email)?.[1]?.toLowerCase();
  if (!emailPattern.test(email)) {
    report(line, `${email} is not an email address`);
    valid = false;
  } else if (noReplyLogin !== undefined && noReplyLogin !== login.toLowerCase()) {
    report(line, `${email} is the no-reply address of ${noReplyLogin}`);
    valid = false;
  }
  return valid ? { line, email: email.toLowerCase(), login: login.toLowerCase() } : undefined;
}

function readPeople(lines: readonly Line[], after: Line, report: Report): Person[] {
  if (lines.length === 0) report(after, `expected at least one "${personShape}" line after "${after.text}"`);
  const people: Person[] = [];
  const seen = new Set<string>();
  for (const line of lines) {
    const person = readPerson(line, report);
    if (person === undefined) continue;
    if (seen.has(person.email)) report(line, `${person.email} is listed twice`);
    seen.add(person.email);
    people.push(person);
  }
  return people;
}

function expectLine(lines: readonly Line[], index: number, expected: string, report: Report): boolean {
  const line = lines[index];
  if (line === undefined) report(undefined, `ends before "${expected}"`);
  else if (line.text !== expected) report(line, `expected "${expected}"`);
  return line?.text === expected;
}

export function parseSignature(path: string, text: string, today: string): ParsedSignature {
  const location = signatureLocation(path);
  if (location === undefined) return { problems: [`${path}: ${namingRule}`] };
  const problems: string[] = [];
  const report: Report = (line, message) =>
    problems.push(line === undefined ? `${path}: ${message}` : `${path}:${line.number}: ${message}`);
  const lines = meaningfulLines(text);
  const [datedPlace] = lines;
  if (datedPlace === undefined) return { problems: [`${path}: is empty; a signature starts with "<country>, <YYYY-MM-DD>"`] };
  checkDatedPlace(datedPlace, today, report);

  if (location.kind === "individual") {
    const complete =
      expectLine(lines, 1, individualStatement, report) &&
      expectLine(lines, 2, individualDeclaration, report) &&
      expectLine(lines, 3, signedLine, report);
    if (!complete) return { problems };
    const people = readPeople(lines.slice(4), lines[3] ?? datedPlace, report);
    for (const person of people) {
      if (person.login !== location.slug) {
        report(
          person.line,
          `names the GitHub login ${person.login}, but an individual signature names only the login in its file name (${location.slug})`,
        );
      }
    }
    if (problems.length > 0) return { problems };
    return { signature: { path, kind: "individual", emails: people.map((person) => person.email), logins: [location.slug] } };
  }

  const statement = lines[1];
  if (statement === undefined || !corporateStatement.test(statement.text)) {
    report(statement, 'expected "<Company> agrees to the terms of the ERP-AI-Pro Corporate Contributor Licence Agreement v1.0."');
    return { problems };
  }
  if (!expectLine(lines, 2, corporateDeclaration, report) || !expectLine(lines, 3, signedLine, report)) return { problems };
  const signatoryLine = lines[4];
  if (signatoryLine === undefined || signatoryLine.text === contributorsLine) {
    report(signatoryLine, `expected the signatory's "${personShape}" line after "${signedLine}"`);
    return { problems };
  }
  const signatory = readPerson(signatoryLine, report);
  const listHeading = lines[5];
  if (listHeading === undefined) {
    report(undefined, `ends before "${contributorsLine}"`);
    return { problems };
  }
  if (listHeading.text !== contributorsLine) {
    report(
      listHeading,
      personPattern.test(listHeading.text)
        ? `a corporate signature has exactly one signatory line, followed by "${contributorsLine}"`
        : `expected "${contributorsLine}"`,
    );
    return { problems };
  }
  const contributors = readPeople(lines.slice(6), listHeading, report);
  if (problems.length > 0 || signatory === undefined) return { problems };
  return {
    signature: {
      path,
      kind: "corporate",
      emails: contributors.map((person) => person.email),
      logins: [...new Set(contributors.map((person) => person.login))],
      signatory: { email: signatory.email, login: signatory.login },
    },
  };
}

function signatoryLogin(text: string): string | undefined {
  const line = meaningfulLines(text)[4];
  return line === undefined ? undefined : personPattern.exec(line.text)?.groups?.login?.toLowerCase();
}

function namedEmails(signature: Signature): readonly string[] {
  return signature.kind === "corporate" ? [...new Set([signature.signatory.email, ...signature.emails])] : signature.emails;
}

function exemptEmailOwners(exemptions: Exemptions): Map<string, string> {
  const owners = new Map<string, string>();
  for (const exemption of exemptions.values()) {
    for (const email of exemption.emails) owners.set(email, exemption.login);
  }
  return owners;
}

export function readSignatures(files: readonly SignatureFile[], today: string, exemptions: Exemptions): SignatureCheck {
  const signatures: Signature[] = [];
  const problems: string[] = [];
  const exemptOwners = exemptEmailOwners(exemptions);
  const signedIn = new Map<string, string>();
  const ordered = files.filter((file) => isSignaturePath(file.path)).sort((a, b) => (a.path < b.path ? -1 : a.path > b.path ? 1 : 0));
  for (const file of ordered) {
    const parsed = parseSignature(file.path, file.text, today);
    if ("problems" in parsed) {
      problems.push(...parsed.problems);
      continue;
    }
    signatures.push(parsed.signature);
    for (const email of namedEmails(parsed.signature)) {
      const owner = exemptOwners.get(email);
      if (owner !== undefined)
        problems.push(`${file.path}: ${email} belongs to the exempt account ${owner}, and an exempt email is never signed`);
      const earlier = signedIn.get(email);
      if (earlier === undefined) signedIn.set(email, file.path);
      else problems.push(`${file.path}: ${email} is already signed in ${earlier}`);
    }
  }
  return { signatures, problems };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function hasExactKeys(value: Record<string, unknown>, keys: readonly string[]): boolean {
  const actual = Object.keys(value);
  return actual.length === keys.length && keys.every((key) => actual.includes(key));
}

export function readExemptions(text: string): Exemptions {
  let document: unknown;
  try {
    document = JSON.parse(text);
  } catch (error) {
    throw new Error(`is not valid JSON (${(error as Error).message})`);
  }
  if (!isRecord(document) || !hasExactKeys(document, ["exemptPullRequestAuthors"]) || !isRecord(document.exemptPullRequestAuthors)) {
    throw new Error('holds exactly one key, "exemptPullRequestAuthors", whose value maps each exempt login to its reason and emails');
  }
  const exemptions = new Map<string, Exemption>();
  const owners = new Map<string, string>();
  for (const [login, entry] of Object.entries(document.exemptPullRequestAuthors)) {
    if (!exemptLoginPattern.test(login)) throw new Error(`"${login}" is not a GitHub login`);
    if (exemptions.has(login.toLowerCase())) throw new Error(`${login} is listed twice`);
    if (!isRecord(entry) || !hasExactKeys(entry, ["reason", "emails"])) throw new Error(`${login} holds exactly "reason" and "emails"`);
    const { reason, emails } = entry;
    if (typeof reason !== "string" || reason.trim() === "") throw new Error(`${login}: "reason" is a non-empty string`);
    if (!Array.isArray(emails) || emails.length === 0) throw new Error(`${login}: "emails" is a non-empty array of email addresses`);
    const normalised: string[] = [];
    for (const email of emails) {
      if (typeof email !== "string" || !exemptEmailPattern.test(email))
        throw new Error(`${login}: ${JSON.stringify(email)} is not an email address`);
      const lower = email.toLowerCase();
      const owner = owners.get(lower);
      if (owner !== undefined)
        throw new Error(owner === login ? `${login}: ${email} is listed twice` : `${email} is listed for both ${owner} and ${login}`);
      owners.set(lower, login);
      normalised.push(lower);
    }
    exemptions.set(login.toLowerCase(), { login, reason, emails: normalised });
  }
  return exemptions;
}

export function parseNameStatus(output: string): Change[] {
  const fields = output.split("\0");
  const changes: Change[] = [];
  for (let index = 0; index + 1 < fields.length; index += 2) {
    const status = fields[index] ?? "";
    const path = fields[index + 1] ?? "";
    if (status === "" || path === "") break;
    changes.push({ status, path });
  }
  return changes;
}

function versionOf(path: string, files: readonly SignatureFile[]): string | undefined {
  return files.find((file) => file.path === path)?.text;
}

function changeProblem(change: Change, prAuthor: string, exempt: boolean, versions: SignatureVersions): string | undefined {
  const kind = change.status.charAt(0);
  if (kind === "D") return `${change.path}: a signature is never deleted or renamed`;
  if (kind !== "A" && kind !== "M") return `${change.path}: a signature is only added or edited (git status ${change.status})`;
  if (exempt) return undefined;
  const author = prAuthor.toLowerCase();
  const location = signatureLocation(change.path);
  if (location?.kind === "individual") {
    if (location.slug === author) return undefined;
    return `${change.path}: this pull request is by ${prAuthor}, who adds or edits only docs/cla/individual/${author}.md`;
  }
  if (location?.kind !== "corporate") return undefined;
  const base = versionOf(change.path, versions.base);
  if (base === undefined && kind === "A") {
    const head = versionOf(change.path, versions.head);
    if (head !== undefined && signatoryLogin(head) === author) return undefined;
    return `${change.path}: this pull request is by ${prAuthor}, and a corporate signature is added only by the signatory it names`;
  }
  if (base !== undefined && signatoryLogin(base) === author) return undefined;
  return `${change.path}: this pull request is by ${prAuthor}, and a corporate signature is edited only by the signatory the base branch names`;
}

export function signatureChangeProblems(
  changes: readonly Change[],
  prAuthor: string,
  exemptions: Exemptions,
  versions: SignatureVersions,
): string[] {
  const exempt = exemptions.has(prAuthor.toLowerCase());
  return changes.flatMap((change) => {
    const problem = isSignaturePath(change.path) ? changeProblem(change, prAuthor, exempt, versions) : undefined;
    return problem === undefined ? [] : [problem];
  });
}

export function pullRequestAuthorProblem(prAuthor: string, signatures: readonly Signature[], exemptions: Exemptions): string | undefined {
  const author = prAuthor.toLowerCase();
  if (exemptions.has(author) || signatures.some((signature) => signature.logins.includes(author))) return undefined;
  return `${prAuthor} opened this pull request but has not signed the contributor licence agreement`;
}

function trailerEmail(value: string): string {
  const unfolded = value.replace(/\r?\n[ \t]+/g, " ").trim();
  return trailerIdentityPattern.exec(unfolded)?.[1] ?? unfolded;
}

export function parseCommitLog(output: string): Commit[] {
  return output
    .split("\x1d")
    .map((record) => record.replace(/^\s+/, ""))
    .filter((record) => record !== "")
    .map((record) => {
      const [sha = "", author = "", trailers = ""] = record.split("\x1f");
      return {
        sha,
        author: author.trim(),
        coAuthors: trailers
          .split("\x1e")
          .map(trailerEmail)
          .filter((email) => email !== ""),
      };
    });
}

export function uncoveredCommits(
  commits: readonly Commit[],
  signatures: readonly Signature[],
  exemptions: Exemptions,
  prAuthor: string,
): UncoveredEmail[] {
  const authorIsExempt = exemptions.has(prAuthor.toLowerCase());
  const exemptEmails = new Set([...exemptions.values()].flatMap((exemption) => exemption.emails));
  const signed = new Set(signatures.flatMap((signature) => signature.emails));
  const isCovered = (email: string, role: CommitRole): boolean => {
    const normalised = email.toLowerCase();
    return exemptEmails.has(normalised) ? role === "co-author" || authorIsExempt : signed.has(normalised);
  };
  const uncovered = new Map<string, UncoveredEmail>();
  const check = (sha: string, email: string, role: CommitRole): void => {
    if (!isCovered(email, role)) uncovered.set(`${sha}\n${email.toLowerCase()}\n${role}`, { sha, email, role });
  };
  for (const commit of commits) {
    check(commit.sha, commit.author, "author");
    for (const coAuthor of commit.coAuthors) check(commit.sha, coAuthor, "co-author");
  }
  return [...uncovered.values()];
}
