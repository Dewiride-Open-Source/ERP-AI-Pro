import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { test } from "node:test";

import {
  commitIdentity,
  type Commit,
  type Exemptions,
  isSignaturePath,
  parseNameStatus,
  parseSignature,
  pullRequestAuthorProblem,
  readExemptions,
  readSignatures,
  type Signature,
  signatureChangeProblems,
  uncoveredCommits,
} from "../lib/cla.ts";
import { repoRoot, toRepoPath, walk } from "../lib/walk.ts";

const cli = join(repoRoot, "scripts", "checks", "cla.ts");
const today = "2026-10-01";
const owner = "jagdish-kumawat";
const ownerEmail = "jagdish.kumawat@dewiride.com";
const ownerNoReply = "109734266+jagdish-kumawat@users.noreply.github.com";
const dependabot = "dependabot[bot]";
const dependabotEmail = "49699333+dependabot[bot]@users.noreply.github.com";

const exemptionDocument = JSON.stringify({
  exemptPullRequestAuthors: {
    [owner]: { reason: "the copyright holder", emails: [ownerNoReply, ownerEmail] },
    [dependabot]: { reason: "Dependabot version and security updates", emails: [dependabotEmail] },
  },
});
const exemptions: Exemptions = readExemptions(exemptionDocument);
const noExemptions: Exemptions = new Map();

const individual = (lines: { date?: string; statement?: string; declaration?: string; people?: readonly string[] } = {}): string =>
  [
    `India, ${lines.date ?? "2026-09-29"}`,
    "",
    lines.statement ?? "I hereby agree to the terms of the ERP-AI-Pro Individual Contributor Licence Agreement v1.0.",
    "",
    lines.declaration ?? "I declare that I am authorised and able to make this agreement and sign this declaration.",
    "",
    "Signed,",
    "",
    ...(lines.people ?? ["Jane Doe jane@example.com https://github.com/janedoe"]),
    "",
  ].join("\n");

const corporate = (signatory: readonly string[], contributors: readonly string[]): string =>
  [
    "India, 2026-09-29",
    "",
    "Example Private Limited agrees to the terms of the ERP-AI-Pro Corporate Contributor Licence Agreement v1.0.",
    "",
    "I declare that I am authorised and able to make this agreement and sign this declaration on behalf of the company named above.",
    "",
    "Signed,",
    "",
    ...signatory,
    "",
    "List of contributors:",
    "",
    ...contributors,
    "",
  ].join("\n");

const problemsOf = (path: string, text: string, date = today): readonly string[] => {
  const parsed = parseSignature(path, text, date);
  return "problems" in parsed ? parsed.problems : [];
};

const signatureOf = (path: string, text: string): Signature => {
  const parsed = parseSignature(path, text, today);
  assert.ok("signature" in parsed, "problems" in parsed ? parsed.problems.join("\n") : "");
  return parsed.signature;
};

const commit = (sha: string, author: string, ...coAuthors: string[]): Commit => ({ sha, author, coAuthors });

const janeSignature = (): Signature => signatureOf("docs/cla/individual/janedoe.md", individual());

const ashaLine = "Asha Rao asha.rao@example.com https://github.com/asharao";
const raviLine = "Ravi Kumar ravi.kumar@example.com https://github.com/ravikumar";
const malloryLine = "Mallory Moe mallory@example.com https://github.com/mallory";
const unsignedAuthor = (login: string): string => `${login} opened this pull request but has not signed the contributor licence agreement`;

test("a well-formed individual signature yields its emails and login", () => {
  const text = individual({
    people: [
      "Jane Doe Jane@Example.com https://github.com/JaneDoe",
      "Jane Doe 12345+janedoe@users.noreply.github.com https://github.com/janedoe",
    ],
  });
  const signature = signatureOf("docs/cla/individual/janedoe.md", text);
  assert.deepEqual(signature, {
    path: "docs/cla/individual/janedoe.md",
    kind: "individual",
    emails: ["jane@example.com", "12345+janedoe@users.noreply.github.com"],
    logins: ["janedoe"],
  });
  assert.deepEqual(signatureOf("docs/cla/individual/janedoe.md", `\uFEFF${text.replaceAll("\n", "\r\n")}`), signature);
  assert.deepEqual(problemsOf("docs/cla/individual/janedoe.md", individual({ date: today })), []);
});

test("a signature whose first statement differs is refused", () => {
  const path = "docs/cla/individual/janedoe.md";
  const statement = problemsOf(
    path,
    individual({ statement: "I agree to the terms of the ERP-AI-Pro Individual Contributor Licence Agreement v1.0." }),
  );
  assert.deepEqual(statement, [
    `${path}:3: expected "I hereby agree to the terms of the ERP-AI-Pro Individual Contributor Licence Agreement v1.0."`,
  ]);
  const version = problemsOf(
    path,
    individual({ statement: "I hereby agree to the terms of the ERP-AI-Pro Individual Contributor Licence Agreement v2.0." }),
  );
  assert.equal(version.length, 1);
  const declaration = problemsOf(path, individual({ declaration: "I am authorised to sign." }));
  assert.match(
    declaration.join("\n"),
    /:5: expected "I declare that I am authorised and able to make this agreement and sign this declaration\."/,
  );
  const corporateText = corporate(
    ["Asha Rao asha.rao@example.com https://github.com/asharao"],
    ["Asha Rao asha.rao@example.com https://github.com/asharao"],
  );
  assert.match(
    problemsOf("docs/cla/corporate/example.md", corporateText.replace("agrees to the terms", "accepts the terms")).join("\n"),
    /expected "<Company> agrees to the terms of the ERP-AI-Pro Corporate Contributor Licence Agreement v1\.0\."/,
  );
});

test("a login that differs from the file name is refused", () => {
  const path = "docs/cla/individual/janedoe.md";
  assert.deepEqual(problemsOf(path, individual({ people: ["Jane Doe jane@example.com https://github.com/JaneDoe"] })), []);
  assert.deepEqual(problemsOf(path, individual({ people: ["John Roe john@example.com https://github.com/johnroe"] })), [
    `${path}:9: names the GitHub login johnroe, but an individual signature names only the login in its file name (janedoe)`,
  ]);
  const bot = problemsOf(
    "docs/cla/individual/dependabot.md",
    individual({ people: [`Dependabot ${dependabotEmail} https://github.com/${dependabot}`] }),
  );
  assert.match(bot.join("\n"), /dependabot\[bot\] is a bot account, and a bot never signs/);
  assert.match(
    problemsOf(path, individual({ people: ["Jane Doe jane@example.com https://github.com/-janedoe"] })).join("\n"),
    /-janedoe is not a GitHub login/,
  );
});

test("a missing field, a malformed email and a malformed or future date are refused", () => {
  const path = "docs/cla/individual/janedoe.md";
  const shape = /expected "<name> <email> https:\/\/github\.com\/<login>"/;
  assert.match(problemsOf(path, individual({ people: ["jane@example.com https://github.com/janedoe"] })).join("\n"), shape);
  assert.match(problemsOf(path, individual({ people: ["Jane Doe jane@example.com"] })).join("\n"), shape);
  assert.match(
    problemsOf(path, individual({ people: [] })).join("\n"),
    /expected at least one "<name> <email> https:\/\/github\.com\/<login>" line after "Signed,"/,
  );
  assert.match(
    problemsOf(path, individual({ people: ["Jane Doe jane.example.com https://github.com/janedoe"] })).join("\n"),
    /jane\.example\.com is not an email address/,
  );
  assert.match(
    problemsOf(path, individual({ people: ["Jane Doe jane@example https://github.com/janedoe"] })).join("\n"),
    /jane@example is not an email address/,
  );
  assert.match(problemsOf(path, individual({ date: "2026-02-30" })).join("\n"), /:1: 2026-02-30 is not a calendar date/);
  assert.match(problemsOf(path, individual({ date: "2026-10-02" })).join("\n"), /:1: 2026-10-02 is later than today \(2026-10-01\)/);
  assert.match(
    problemsOf(path, individual().replace("India, 2026-09-29", "India 2026-09-29")).join("\n"),
    /:1: expected "<country>, <YYYY-MM-DD>"/,
  );
  assert.match(
    problemsOf(path, individual().replace("India, 2026-09-29", "India, 29-09-2026")).join("\n"),
    /:1: expected "<country>, <YYYY-MM-DD>"/,
  );
  assert.match(problemsOf(path, individual().replace("Signed,", "Signed")).join("\n"), /:7: expected "Signed,"/);
  assert.match(problemsOf(path, "India, 2026-09-29\n").join("\n"), /ends before "I hereby agree/);
  assert.match(problemsOf(path, "\n\n").join("\n"), /is empty/);
  assert.match(
    problemsOf(
      path,
      individual({
        people: ["Jane Doe jane@example.com https://github.com/janedoe", "Jane Doe JANE@example.com https://github.com/janedoe"],
      }),
    ).join("\n"),
    /jane@example\.com is listed twice/,
  );
});

test("a corporate signature lists every contributor and needs at least one", () => {
  const path = "docs/cla/corporate/example-private-limited.md";
  const signatory = ["Asha Rao asha.rao@example.com https://github.com/asharao"];
  const contributors = [
    "Ravi Kumar ravi.kumar@example.com https://github.com/ravikumar",
    "Ravi Kumar 777+ravikumar@users.noreply.github.com https://github.com/ravikumar",
  ];
  assert.deepEqual(signatureOf(path, corporate(signatory, contributors)), {
    path,
    kind: "corporate",
    emails: ["ravi.kumar@example.com", "777+ravikumar@users.noreply.github.com"],
    logins: ["ravikumar"],
    signatory: { email: "asha.rao@example.com", login: "asharao" },
  });
  assert.deepEqual(signatureOf(path, corporate(signatory, [...signatory, ...contributors])).emails, [
    "asha.rao@example.com",
    "ravi.kumar@example.com",
    "777+ravikumar@users.noreply.github.com",
  ]);
  assert.match(
    problemsOf(path, corporate(signatory, [])).join("\n"),
    /expected at least one "<name> <email> https:\/\/github\.com\/<login>" line after "List of contributors:"/,
  );
  assert.match(
    problemsOf(path, corporate([...signatory, "Ravi Kumar ravi.kumar@example.com https://github.com/ravikumar"], contributors)).join("\n"),
    /exactly one signatory line, followed by "List of contributors:"/,
  );
  assert.match(
    problemsOf(path, corporate([], contributors)).join("\n"),
    /:10: expected the signatory's "<name> <email> https:\/\/github\.com\/<login>" line after "Signed,"/,
  );
  assert.match(
    problemsOf(path, corporate(signatory, contributors).replace("List of contributors:", "Contributors:")).join("\n"),
    /:11: expected "List of contributors:"/,
  );
  assert.match(
    problemsOf(path, corporate(signatory, [contributors[0] ?? "", contributors[0] ?? ""])).join("\n"),
    /ravi\.kumar@example\.com is listed twice/,
  );
});

test("a GitHub no-reply address names the login on its line", () => {
  const path = "docs/cla/individual/janedoe.md";
  assert.deepEqual(
    problemsOf(
      path,
      individual({
        people: [
          "Jane Doe 12345+JaneDoe@users.noreply.github.com https://github.com/janedoe",
          "Jane Doe janedoe@users.noreply.github.com https://github.com/janedoe",
        ],
      }),
    ),
    [],
  );
  assert.deepEqual(
    problemsOf(path, individual({ people: ["Jane Doe 67890+johnroe@users.noreply.github.com https://github.com/janedoe"] })),
    [`${path}:9: 67890+johnroe@users.noreply.github.com is the no-reply address of johnroe`],
  );
  const corporatePath = "docs/cla/corporate/example.md";
  assert.deepEqual(
    problemsOf(
      corporatePath,
      corporate(
        ["Asha Rao 1+RaviKumar@users.noreply.github.com https://github.com/asharao"],
        ["Ravi Kumar johnroe@users.noreply.github.com https://github.com/ravikumar"],
      ),
    ),
    [
      `${corporatePath}:9: 1+RaviKumar@users.noreply.github.com is the no-reply address of ravikumar`,
      `${corporatePath}:13: johnroe@users.noreply.github.com is the no-reply address of johnroe`,
    ],
  );
});

test("a file outside the naming rule and an email signed twice are refused", () => {
  const files = [
    { path: "docs/cla/individual/JaneDoe.md", text: individual() },
    { path: "docs/cla/individual/nested/janedoe.md", text: individual() },
    { path: "docs/cla/individual/janedoe.txt", text: individual() },
    { path: "docs/cla/individual/jane_doe.md", text: individual() },
    { path: "docs/cla/corporate/Example_Company.md", text: individual() },
    { path: "docs/cla/sign-cla.md", text: "not a signature" },
    { path: "docs/cla/exempt.json", text: exemptionDocument },
  ];
  const rule = ": a signature is docs/cla/individual/<lowercase-github-login>.md or docs/cla/corporate/<company-slug>.md";
  assert.deepEqual(readSignatures(files, today, exemptions), {
    signatures: [],
    problems: [
      `docs/cla/corporate/Example_Company.md${rule}`,
      `docs/cla/individual/JaneDoe.md${rule}`,
      `docs/cla/individual/jane_doe.md${rule}`,
      `docs/cla/individual/janedoe.txt${rule}`,
      `docs/cla/individual/nested/janedoe.md${rule}`,
    ],
  });

  const twice = readSignatures(
    [
      { path: "docs/cla/individual/janedoe.md", text: individual() },
      {
        path: "docs/cla/corporate/example.md",
        text: corporate(
          ["Asha Rao asha.rao@example.com https://github.com/asharao"],
          ["Jane Doe JANE@example.com https://github.com/janedoe"],
        ),
      },
    ],
    today,
    exemptions,
  );
  assert.equal(twice.signatures.length, 2);
  assert.deepEqual(twice.problems, ["docs/cla/individual/janedoe.md: jane@example.com is already signed in docs/cla/corporate/example.md"]);

  const exempt = readSignatures(
    [
      {
        path: `docs/cla/individual/${owner}.md`,
        text: individual({ people: [`Jagdish Kumawat ${ownerEmail} https://github.com/${owner}`] }),
      },
    ],
    today,
    exemptions,
  );
  assert.deepEqual(exempt.problems, [
    `docs/cla/individual/${owner}.md: ${ownerEmail} belongs to the exempt account ${owner}, and an exempt email is never signed`,
  ]);
  assert.deepEqual(readSignatures([{ path: "docs/cla/individual/janedoe.md", text: individual() }], today, noExemptions).problems, []);
});

test("a corporate signatory's email is never exempt and never signed in another file", () => {
  const path = "docs/cla/corporate/example.md";
  const exempt = readSignatures(
    [{ path, text: corporate([`Jagdish Kumawat ${ownerEmail} https://github.com/${owner}`], [raviLine]) }],
    today,
    exemptions,
  );
  assert.deepEqual(exempt.problems, [`${path}: ${ownerEmail} belongs to the exempt account ${owner}, and an exempt email is never signed`]);
  const twice = readSignatures(
    [
      { path, text: corporate(["Jane Doe jane@example.com https://github.com/janedoe"], [raviLine]) },
      { path: "docs/cla/individual/janedoe.md", text: individual() },
    ],
    today,
    exemptions,
  );
  assert.deepEqual(twice.problems, ["docs/cla/individual/janedoe.md: jane@example.com is already signed in docs/cla/corporate/example.md"]);
  const listed = readSignatures([{ path, text: corporate([ashaLine], [ashaLine, raviLine]) }], today, exemptions);
  assert.deepEqual(listed.problems, []);
  assert.deepEqual(listed.signatures[0]?.emails, ["asha.rao@example.com", "ravi.kumar@example.com"]);
  const unlisted = readSignatures([{ path, text: corporate([ashaLine], [raviLine]) }], today, exemptions);
  assert.deepEqual(unlisted.signatures[0]?.emails, ["ravi.kumar@example.com"]);
  assert.deepEqual(uncoveredCommits([commit("s1", "asha.rao@example.com")], unlisted.signatures, exemptions, "ravikumar"), [
    { sha: "s1", email: "asha.rao@example.com", role: "author" },
  ]);
});

test("a malformed exemption document is refused", () => {
  const entry = { reason: "the copyright holder", emails: [ownerEmail] };
  const document = (value: unknown): string => JSON.stringify(value);
  for (const [text, message] of [
    ["{", /is not valid JSON/],
    [document([]), /holds exactly one key, "exemptPullRequestAuthors"/],
    [document({ exemptPullRequestAuthors: [] }), /holds exactly one key, "exemptPullRequestAuthors"/],
    [document({ exemptPullRequestAuthors: {}, extra: true }), /holds exactly one key, "exemptPullRequestAuthors"/],
    [document({ exemptPullRequestAuthors: { "not a login": entry } }), /"not a login" is not a GitHub login/],
    [document({ exemptPullRequestAuthors: { [owner]: { reason: "x" } } }), /holds exactly "reason" and "emails"/],
    [document({ exemptPullRequestAuthors: { [owner]: { ...entry, note: "x" } } }), /holds exactly "reason" and "emails"/],
    [document({ exemptPullRequestAuthors: { [owner]: { ...entry, reason: " " } } }), /"reason" is a non-empty string/],
    [document({ exemptPullRequestAuthors: { [owner]: { ...entry, emails: [] } } }), /"emails" is a non-empty array/],
    [document({ exemptPullRequestAuthors: { [owner]: { ...entry, emails: ["owner"] } } }), /"owner" is not an email address/],
    [document({ exemptPullRequestAuthors: { [owner]: { ...entry, emails: [ownerEmail, ownerEmail.toUpperCase()] } } }), /is listed twice/],
    [document({ exemptPullRequestAuthors: { [owner]: entry, "Jagdish-Kumawat": entry } }), /Jagdish-Kumawat is listed twice/],
    [
      document({ exemptPullRequestAuthors: { [owner]: entry, [dependabot]: entry } }),
      /is listed for both jagdish-kumawat and dependabot\[bot\]/,
    ],
  ] as const) {
    assert.throws(() => readExemptions(text), message, text);
  }
  const parsed = readExemptions(
    document({
      exemptPullRequestAuthors: { "Jagdish-Kumawat": { reason: "the copyright holder", emails: ["Jagdish.Kumawat@Dewiride.com"] } },
    }),
  );
  assert.deepEqual(
    [...parsed.entries()],
    [["jagdish-kumawat", { login: "Jagdish-Kumawat", reason: "the copyright holder", emails: [ownerEmail] }]],
  );
});

test("every exempt email is covered in a pull request opened by an exempt login", () => {
  const ownerCommits = [commit("a1", ownerNoReply), commit("a2", ownerEmail.toUpperCase(), ownerNoReply)];
  assert.deepEqual(uncoveredCommits(ownerCommits, [], exemptions, owner), []);
  assert.deepEqual(uncoveredCommits(ownerCommits, [], exemptions, "Jagdish-Kumawat"), []);
  assert.deepEqual(uncoveredCommits([commit("b1", dependabotEmail, dependabotEmail)], [], exemptions, dependabot), []);
  assert.deepEqual(uncoveredCommits([commit("b2", dependabotEmail), commit("b3", ownerNoReply)], [], exemptions, dependabot), []);
  assert.deepEqual(uncoveredCommits([commit("b4", "jane@example.com")], [], exemptions, dependabot), [
    { sha: "b4", email: "jane@example.com", role: "author" },
  ]);
});

test("an exempt email authors a commit only in a pull request opened by an exempt login", () => {
  const signatures = [janeSignature()];
  assert.deepEqual(uncoveredCommits([commit("c1", "jane@example.com", ownerEmail)], signatures, exemptions, "janedoe"), []);
  assert.equal(pullRequestAuthorProblem("janedoe", signatures, exemptions), undefined);
  assert.deepEqual(uncoveredCommits([commit("c2", ownerNoReply, "jane@example.com")], signatures, exemptions, "janedoe"), [
    { sha: "c2", email: ownerNoReply, role: "author" },
  ]);
  assert.deepEqual(uncoveredCommits([commit("c3", dependabotEmail, ownerEmail)], [], exemptions, "Dependabot"), [
    { sha: "c3", email: dependabotEmail, role: "author" },
  ]);
  assert.deepEqual(uncoveredCommits([commit("c5", "jane@example.com", ownerNoReply)], [], exemptions, "janedoe"), [
    { sha: "c5", email: "jane@example.com", role: "author" },
  ]);
  assert.equal(pullRequestAuthorProblem("janedoe", [], exemptions), unsignedAuthor("janedoe"));
  const borrowed: Signature = {
    path: "docs/cla/individual/janedoe.md",
    kind: "individual",
    emails: ["jane@example.com", ownerEmail],
    logins: ["janedoe"],
  };
  assert.deepEqual(uncoveredCommits([commit("c4", ownerEmail)], [borrowed], exemptions, "janedoe"), [
    { sha: "c4", email: ownerEmail, role: "author" },
  ]);
});

test("a signed author is covered whatever the case of the email", () => {
  const signatures = [janeSignature()];
  assert.deepEqual(
    uncoveredCommits([commit("d1", "Jane@Example.COM"), commit("d2", "jane@example.com")], signatures, exemptions, "janedoe"),
    [],
  );
  assert.deepEqual(uncoveredCommits([commit("d3", "jane@example.org")], signatures, exemptions, "janedoe"), [
    { sha: "d3", email: "jane@example.org", role: "author" },
  ]);
  assert.deepEqual(uncoveredCommits([commit("d4", "jane@example.com")], signatures, exemptions, owner), []);
});

test("a corporate contributor is covered", () => {
  const signature = signatureOf(
    "docs/cla/corporate/example.md",
    corporate(
      ["Asha Rao asha.rao@example.com https://github.com/asharao"],
      ["Ravi Kumar ravi.kumar@example.com https://github.com/ravikumar"],
    ),
  );
  assert.deepEqual(uncoveredCommits([commit("e1", "Ravi.Kumar@example.com")], [signature], exemptions, "ravikumar"), []);
  assert.deepEqual(uncoveredCommits([commit("e2", "asha.rao@example.com")], [signature], exemptions, "asharao"), [
    { sha: "e2", email: "asha.rao@example.com", role: "author" },
  ]);
});

test("a pull request author who is not exempt is named by a signature", () => {
  const jane = janeSignature();
  const company = signatureOf("docs/cla/corporate/example.md", corporate([ashaLine], [raviLine]));
  assert.deepEqual(uncoveredCommits([commit("h1", "jane@example.com")], [jane], exemptions, "johnroe"), []);
  assert.equal(pullRequestAuthorProblem("johnroe", [jane, company], exemptions), unsignedAuthor("johnroe"));
  assert.equal(pullRequestAuthorProblem("JaneDoe", [jane, company], exemptions), undefined);
  assert.equal(pullRequestAuthorProblem("Jagdish-Kumawat", [], exemptions), undefined);
  assert.equal(pullRequestAuthorProblem(dependabot, [], exemptions), undefined);
  assert.equal(pullRequestAuthorProblem("RaviKumar", [jane, company], exemptions), undefined);
  assert.equal(pullRequestAuthorProblem("asharao", [jane, company], exemptions), unsignedAuthor("asharao"));
});

test("an unsigned co-author is not covered", () => {
  const signatures = [janeSignature()];
  assert.deepEqual(
    uncoveredCommits([commit("f1", "jane@example.com", "ravi@example.com", "ravi@example.com")], signatures, exemptions, "janedoe"),
    [{ sha: "f1", email: "ravi@example.com", role: "co-author" }],
  );
  const log = [
    commitIdentity("f2", "jane@example.com\n", "Ravi Kumar <Ravi@Example.com>\0Jane Doe\n"),
    commitIdentity("f3", "jane@example.com\n", "\n"),
  ];
  assert.deepEqual(log, [commit("f2", "jane@example.com", "Ravi@Example.com", "Jane Doe"), commit("f3", "jane@example.com")]);
  assert.deepEqual(uncoveredCommits(log, signatures, exemptions, "janedoe"), [
    { sha: "f2", email: "Ravi@Example.com", role: "co-author" },
    { sha: "f2", email: "Jane Doe", role: "co-author" },
  ]);
  assert.deepEqual(uncoveredCommits([commit("f4", "ravi@example.com", "jane@example.com")], signatures, exemptions, "janedoe"), [
    { sha: "f4", email: "ravi@example.com", role: "author" },
  ]);
});

test("a co-author trailer is unfolded and holds exactly one address, or it is reported whole", () => {
  const signatures = [
    janeSignature(),
    signatureOf("docs/cla/individual/ravikumar.md", individual({ people: ["Ravi Kumar ravi@example.com https://github.com/ravikumar"] })),
  ];
  const log = [
    commitIdentity("g1", "jane@example.com", "Ravi Kumar\n <ravi@example.com>\0Ravi Kumar\r\n\t<Ravi@Example.com> "),
    commitIdentity("g2", "jane@example.com", "Jane Doe <jane@example.com> Ravi Kumar <ravi@example.com>"),
    commitIdentity("g3", "jane@example.com", "Jane Doe <jane@example.com>\n Ravi Kumar <ravi@example.com>"),
    commitIdentity("g4", "jane@example.com", "Ravi Kumar <ravi @example.com>"),
  ];
  assert.deepEqual(log, [
    commit("g1", "jane@example.com", "ravi@example.com", "Ravi@Example.com"),
    commit("g2", "jane@example.com", "Jane Doe <jane@example.com> Ravi Kumar <ravi@example.com>"),
    commit("g3", "jane@example.com", "Jane Doe <jane@example.com> Ravi Kumar <ravi@example.com>"),
    commit("g4", "jane@example.com", "Ravi Kumar <ravi @example.com>"),
  ]);
  assert.deepEqual(uncoveredCommits(log, signatures, exemptions, "janedoe"), [
    { sha: "g2", email: "Jane Doe <jane@example.com> Ravi Kumar <ravi@example.com>", role: "co-author" },
    { sha: "g3", email: "Jane Doe <jane@example.com> Ravi Kumar <ravi@example.com>", role: "co-author" },
    { sha: "g4", email: "Ravi Kumar <ravi @example.com>", role: "co-author" },
  ]);
});

test("a corporate signature is added only by its signatory and edited only by the signatory the base branch names", () => {
  const path = "docs/cla/corporate/example.md";
  const base = [{ path, text: corporate([ashaLine], [ashaLine]) }];
  const edited = [{ path, text: corporate([ashaLine], [ashaLine, raviLine]) }];
  const swapped = [{ path, text: corporate([malloryLine], [ashaLine, malloryLine]) }];
  const added = parseNameStatus(`A\0${path}\0`);
  const modified = parseNameStatus(`M\0${path}\0`);
  const addedOnly = (login: string): string =>
    `${path}: this pull request is by ${login}, and a corporate signature is added only by the signatory it names`;
  const editedOnly = (login: string): string =>
    `${path}: this pull request is by ${login}, and a corporate signature is edited only by the signatory the base branch names`;

  assert.deepEqual(signatureChangeProblems(added, "AshaRao", exemptions, { base: [], head: base }), []);
  assert.deepEqual(signatureChangeProblems(added, "ravikumar", exemptions, { base: [], head: base }), [addedOnly("ravikumar")]);
  assert.deepEqual(signatureChangeProblems(added, "mallory", exemptions, { base: [], head: [] }), [addedOnly("mallory")]);
  assert.deepEqual(signatureChangeProblems(modified, "asharao", exemptions, { base, head: edited }), []);
  assert.deepEqual(signatureChangeProblems(modified, "ravikumar", exemptions, { base, head: edited }), [editedOnly("ravikumar")]);
  assert.deepEqual(signatureChangeProblems(modified, "mallory", exemptions, { base, head: swapped }), [editedOnly("mallory")]);
  assert.deepEqual(signatureChangeProblems(added, "mallory", exemptions, { base, head: swapped }), [editedOnly("mallory")]);
  assert.deepEqual(signatureChangeProblems(modified, "asharao", exemptions, { base: [], head: edited }), [editedOnly("asharao")]);
  assert.deepEqual(signatureChangeProblems(modified, owner, exemptions, { base, head: swapped }), []);
  assert.deepEqual(signatureChangeProblems(parseNameStatus(`D\0${path}\0`), "asharao", exemptions, { base, head: [] }), [
    `${path}: a signature is never deleted or renamed`,
  ]);
});

test("a contributor cannot sign for another login or delete a signature", () => {
  const changes = parseNameStatus(
    [
      "A",
      "docs/cla/individual/johnroe.md",
      "M",
      "docs/cla/individual/janedoe.md",
      "A",
      "docs/cla/corporate/example.md",
      "D",
      "docs/cla/individual/ravikumar.md",
      "M",
      "docs/cla/sign-cla.md",
      "",
    ].join("\0"),
  );
  assert.equal(changes.length, 5);
  const versions = { base: [], head: [{ path: "docs/cla/corporate/example.md", text: corporate([ashaLine], [ashaLine]) }] };
  assert.deepEqual(signatureChangeProblems(changes, "JaneDoe", exemptions, versions), [
    "docs/cla/individual/johnroe.md: this pull request is by JaneDoe, who adds or edits only docs/cla/individual/janedoe.md",
    "docs/cla/corporate/example.md: this pull request is by JaneDoe, and a corporate signature is added only by the signatory it names",
    "docs/cla/individual/ravikumar.md: a signature is never deleted or renamed",
  ]);
  assert.deepEqual(signatureChangeProblems(changes, owner, exemptions, versions), [
    "docs/cla/individual/ravikumar.md: a signature is never deleted or renamed",
  ]);
  const none = { base: [], head: [] };
  assert.deepEqual(signatureChangeProblems(parseNameStatus("A\0docs/cla/individual/janedoe.md\0"), "janedoe", exemptions, none), []);
  assert.deepEqual(signatureChangeProblems(parseNameStatus("T\0docs/cla/individual/janedoe.md\0"), "janedoe", exemptions, none), [
    "docs/cla/individual/janedoe.md: a signature is only added or edited (git status T)",
  ]);
  assert.deepEqual(parseNameStatus(""), []);
});

const temporaryRepository = (t: { after: (fn: () => void) => void }) => {
  const directory = mkdtempSync(join(tmpdir(), "cla-"));
  t.after(() => rmSync(directory, { recursive: true, force: true }));
  const git = (...args: string[]): string =>
    execFileSync("git", ["-C", directory, "-c", "core.autocrlf=false", "-c", "commit.gpgsign=false", ...args], { encoding: "utf8" }).trim();
  const write = (path: string, text: string) => {
    mkdirSync(dirname(join(directory, path)), { recursive: true });
    writeFileSync(join(directory, path), text);
  };
  const commitAs = (email: string, message: string): string => {
    git("add", "--all");
    git(
      "-c",
      "user.name=Committer",
      "-c",
      "user.email=committer@example.com",
      "commit",
      "-q",
      "--author",
      `Someone <${email}>`,
      "-m",
      message,
    );
    return git("rev-parse", "HEAD");
  };
  git("init", "-q");
  write("docs/cla/exempt.json", exemptionDocument);
  write("README.md", "base\n");
  const base = commitAs(ownerEmail, "base");
  const run = (...args: string[]) => spawnSync(process.execPath, [cli, "--root", directory, ...args], { encoding: "utf8" });
  return { directory, git, write, commitAs, base, run };
};

test("the command line fails on an unsigned author and passes once the signature is added", (t) => {
  const repository = temporaryRepository(t);
  repository.write("feature.txt", "feature\n");
  const unsigned = repository.commitAs(
    "jane@example.com",
    "Add a feature\n\nCo-authored-by: Jane Doe <12345+janedoe@users.noreply.github.com>",
  );

  const failing = repository.run("--base", repository.base, "--head", unsigned, "--pull-request-author", "janedoe");
  assert.equal(failing.status, 1, failing.stdout);
  const short = unsigned.slice(0, 7);
  assert.match(failing.stderr, /janedoe opened this pull request but has not signed the contributor licence agreement\./);
  assert.match(failing.stderr, new RegExp(`  ${short} jane@example\\.com \\(author\\)`));
  assert.match(failing.stderr, new RegExp(`  ${short} 12345\\+janedoe@users\\.noreply\\.github\\.com \\(co-author\\)`));
  assert.match(failing.stderr, /docs\/cla\/sign-cla\.md/);

  repository.write(
    "docs/cla/individual/janedoe.md",
    individual({
      people: [
        "Jane Doe jane@example.com https://github.com/janedoe",
        "Jane Doe 12345+janedoe@users.noreply.github.com https://github.com/janedoe",
      ],
    }),
  );
  const signed = repository.commitAs(
    "jane@example.com",
    `Sign the contributor licence agreement\n\nCo-authored-by: Jagdish Kumawat <${ownerEmail}>`,
  );
  const passing = repository.run("--base", repository.base, "--head", signed, "--pull-request-author", "janedoe");
  assert.equal(passing.status, 0, passing.stderr);
  assert.match(passing.stdout, /cla ok: janedoe and every author and co-author of 2 commits are exempt or signed/);

  const otherAuthor = repository.run("--base", repository.base, "--head", signed, "--pull-request-author", "johnroe");
  assert.equal(otherAuthor.status, 1);
  assert.match(otherAuthor.stderr, /docs\/cla\/individual\/janedoe\.md: this pull request is by johnroe/);
  assert.match(otherAuthor.stderr, /johnroe opened this pull request but has not signed the contributor licence agreement\./);

  repository.write("feature.txt", "feature, with a signed author\n");
  const borrowed = repository.commitAs("jane@example.com", "Commit with a signed address");
  const borrowedByOther = repository.run("--base", signed, "--head", borrowed, "--pull-request-author", "johnroe");
  assert.equal(borrowedByOther.status, 1);
  assert.match(borrowedByOther.stderr, /johnroe opened this pull request but has not signed the contributor licence agreement\./);
  assert.doesNotMatch(borrowedByOther.stderr, /\((?:author|co-author)\)/);

  repository.write("feature.txt", "feature, with a folded trailer\n");
  const folded = repository.commitAs(
    "jane@example.com",
    "Credit two people in one trailer\n\nCo-authored-by: Jane Doe <jane@example.com>\n Ravi Kumar <ravi@example.com>",
  );
  const foldedRun = repository.run("--base", signed, "--head", folded, "--pull-request-author", "janedoe");
  assert.equal(foldedRun.status, 1);
  assert.match(
    foldedRun.stderr,
    new RegExp(`  ${folded.slice(0, 7)} Jane Doe <jane@example\\.com> Ravi Kumar <ravi@example\\.com> \\(co-author\\)`),
  );

  const partial = repository.run("--base", repository.base);
  assert.equal(partial.status, 1);
  assert.match(partial.stderr, /--base, --head and --pull-request-author are given together/);
});

test("the command line lets only the base branch's signatory edit a corporate signature", (t) => {
  const repository = temporaryRepository(t);
  const path = "docs/cla/corporate/example-private-limited.md";
  repository.write(path, corporate([ashaLine], [ashaLine]));
  const signed = repository.commitAs(ownerEmail, "Example Private Limited signs");

  repository.write(path, corporate([ashaLine], [ashaLine, raviLine]));
  const edited = repository.commitAs("asha.rao@example.com", "Add Ravi Kumar");
  const bySignatory = repository.run("--base", signed, "--head", edited, "--pull-request-author", "asharao");
  assert.equal(bySignatory.status, 0, bySignatory.stderr);

  repository.git("checkout", "-q", "--detach", signed);
  repository.write(path, corporate([malloryLine], [ashaLine, malloryLine]));
  const swapped = repository.commitAs("mallory@example.com", "Replace the signatory");
  const byOther = repository.run("--base", signed, "--head", swapped, "--pull-request-author", "mallory");
  assert.equal(byOther.status, 1);
  assert.match(
    byOther.stderr,
    /example-private-limited\.md: this pull request is by mallory, and a corporate signature is edited only by the signatory the base branch names/,
  );
  assert.doesNotMatch(byOther.stderr, /opened this pull request|\((?:author|co-author)\)/);
});

test("the command line reads each commit on its own, so separator bytes in a message hide no co-author", (t) => {
  const repository = temporaryRepository(t);
  repository.write("docs/cla/individual/janedoe.md", individual({ people: ["Jane Doe jane@example.com https://github.com/janedoe"] }));
  const signed = repository.commitAs(ownerEmail, "Jane Doe signs");
  repository.write("feature.txt", "feature\n");
  const forged = `Co-authored-by: Jane Doe <jane@example.com>\x1d${"a".repeat(40)}\x1fjane@example.com\x1f\x1fx`;
  const smuggled = repository.commitAs(
    "jane@example.com",
    `Add a feature\n\n${forged}\nCo-authored-by: Unsigned Person <unsigned@example.com>`,
  );
  const run = repository.run("--base", signed, "--head", smuggled, "--pull-request-author", "janedoe");
  assert.equal(run.status, 1, run.stdout);
  assert.match(run.stderr, new RegExp(`  ${smuggled.slice(0, 7)} unsigned@example\\.com \\(co-author\\)`));
});

test("the command line judges signatures as the base branch holds them, with the pull request's own changes on top", (t) => {
  const repository = temporaryRepository(t);
  const path = "docs/cla/corporate/example-private-limited.md";
  repository.write(path, corporate([ashaLine], [ashaLine, malloryLine]));
  const listed = repository.commitAs(ownerEmail, "Example Private Limited signs");

  repository.write("feature.txt", "feature\n");
  const staleBranch = repository.commitAs("mallory@example.com", "Add a feature");
  repository.git("checkout", "-q", "--detach", listed);
  repository.write(path, corporate([ashaLine], [ashaLine]));
  const removed = repository.commitAs("asha.rao@example.com", "Remove Mallory Moe");
  const stale = repository.run("--base", removed, "--head", staleBranch, "--pull-request-author", "mallory");
  assert.equal(stale.status, 1, stale.stdout);
  assert.match(stale.stderr, /mallory opened this pull request but has not signed the contributor licence agreement\./);
  assert.match(stale.stderr, new RegExp(`  ${staleBranch.slice(0, 7)} mallory@example\\.com \\(author\\)`));

  repository.git("checkout", "-q", "--detach", removed);
  repository.write("other.txt", "other\n");
  const earlyBranch = repository.commitAs("ravi.kumar@example.com", "Add another feature");
  const beforeSigning = repository.run("--base", removed, "--head", earlyBranch, "--pull-request-author", "ravikumar");
  assert.equal(beforeSigning.status, 1, beforeSigning.stdout);
  repository.git("checkout", "-q", "--detach", removed);
  repository.write(path, corporate([ashaLine], [ashaLine, raviLine]));
  const added = repository.commitAs("asha.rao@example.com", "Add Ravi Kumar");
  const afterSigning = repository.run("--base", added, "--head", earlyBranch, "--pull-request-author", "ravikumar");
  assert.equal(afterSigning.status, 0, afterSigning.stderr);
});

test("the command line without a range validates only the signature files", (t) => {
  const repository = temporaryRepository(t);
  repository.write("docs/cla/individual/janedoe.md", individual());
  repository.write("docs/cla/notes.txt", "not a signature\n");
  repository.commitAs("unsigned@example.com", "An unsigned author and a signature");

  const clean = repository.run();
  assert.equal(clean.status, 0, clean.stderr);
  assert.match(clean.stdout, /cla ok: 1 individual and 0 corporate signatures/);

  repository.write(".gitignore", "docs/cla/individual/ignored.md\n");
  repository.write("docs/cla/individual/ignored.md", "not a signature\n");
  const ignored = repository.run();
  assert.equal(ignored.status, 0, ignored.stderr);

  repository.write("docs/cla/individual/johnroe.md", individual({ people: ["John Roe jane@example.com https://github.com/johnroe"] }));
  repository.write("docs/cla/individual/ravikumar.md", individual());
  const failing = repository.run();
  assert.equal(failing.status, 1);
  assert.match(
    failing.stderr,
    /docs\/cla\/individual\/johnroe\.md: jane@example\.com is already signed in docs\/cla\/individual\/janedoe\.md/,
  );
  assert.match(failing.stderr, /docs\/cla\/individual\/ravikumar\.md:9: names the GitHub login janedoe/);

  repository.write("docs/cla/exempt.json", "{}");
  const malformed = repository.run();
  assert.equal(malformed.status, 1);
  assert.match(malformed.stderr, /docs\/cla\/exempt\.json: holds exactly one key/);
});

test("the repository signatures and exemptions are well formed", () => {
  const repositoryExemptions = readExemptions(readFileSync(join(repoRoot, "docs", "cla", "exempt.json"), "utf8"));
  assert.ok(repositoryExemptions.size > 0);
  for (const exemption of repositoryExemptions.values()) assert.ok(exemption.emails.length > 0, exemption.login);
  const files = walk(join(repoRoot, "docs", "cla"), () => true)
    .map((file) => ({ path: toRepoPath(file), text: readFileSync(file, "utf8") }))
    .filter((file) => isSignaturePath(file.path));
  assert.deepEqual(readSignatures(files, "9999-12-31", repositoryExemptions).problems, []);
});

test("the signing guide examples pass the checker and each agreement carries the title its statement names", () => {
  const guide = readFileSync(join(repoRoot, "docs", "cla", "sign-cla.md"), "utf8");
  const examples = [...guide.matchAll(/```text\r?\n([\s\S]*?)```/g)].map((match) => match[1] ?? "");
  assert.equal(examples.length, 2);
  const [individualExample = "", corporateExample = ""] = examples;
  assert.equal(signatureOf("docs/cla/individual/janedoe.md", individualExample.replace("2026-10-01", today)).kind, "individual");
  assert.equal(
    signatureOf("docs/cla/corporate/example-private-limited.md", corporateExample.replace("2026-10-01", today)).kind,
    "corporate",
  );
  for (const [file, title] of [
    ["individual-cla-1.0.md", "ERP-AI-Pro Individual Contributor Licence Agreement v1.0"],
    ["corporate-cla-1.0.md", "ERP-AI-Pro Corporate Contributor Licence Agreement v1.0"],
  ] as const) {
    assert.equal(readFileSync(join(repoRoot, "docs", "cla", file), "utf8").split(/\r?\n/)[0], `# ${title}`);
    assert.ok(individualExample.includes(title) || corporateExample.includes(title), title);
  }
});
