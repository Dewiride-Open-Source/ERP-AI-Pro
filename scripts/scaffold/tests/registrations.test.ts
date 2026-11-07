import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";

import type { ScaffoldInput } from "../lib/names.ts";
import {
  domainFilter,
  filterNamesOf,
  filterProjectsOf,
  modulesAllOf,
  projectReferencesOf,
  registerFilterInReadme,
  registerInComposition,
  registerInDomainFilter,
  registerInModulesAll,
  registerInSolution,
  solutionFoldersOf,
} from "../lib/registrations/backend.ts";
import { describeModule, type ScaffoldModule } from "../lib/registrations/module.ts";
import { paths } from "../lib/registrations/paths.ts";
import { flagSeedsOf, registerFlag, registerFlagRow, registerRoutes, routedModulesOf } from "../lib/registrations/repository.ts";
import { insertElement, listLiteral, ordinal, preservingShape } from "../lib/registrations/text.ts";
import {
  changeKeysOf,
  importsOf,
  recordTypeKeysOf,
  registeredNavigationOf,
  registerChangeKey,
  registerInNavigationFixture,
  registerInRegistry,
  registerRecordType,
  registryOf,
} from "../lib/registrations/web.ts";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");

// The edits below run on the repository's own files, so their modules carry names that neither a real module nor the
// scaffolder's proof module will ever take.
const input: ScaffoldInput = {
  domain: "ScaffoldTest",
  module: "Ledger",
  entity: "ScaffoldNote",
  entities: "ScaffoldNotes",
  title: "Scaffold notes",
  description: "Keep short notes about the scaffold.",
  readSentence: "See the scaffold notes.",
  createSentence: "Add scaffold notes.",
  areaTitle: "Scaffold test",
  icon: "NotebookIcon",
};

const newDomain = describeModule(input, { newDomain: true, areaTitle: "Scaffold test" });
const platformModule = describeModule({ ...input, module: "ScaffoldLedger", domain: "Platform", areaTitle: undefined }, { newDomain: false, areaTitle: "Platform" });
const identityModule = describeModule({ ...input, domain: "Identity", module: "ScaffoldTeams", entity: "ScaffoldTeam", entities: "ScaffoldTeams", areaTitle: undefined }, { newDomain: false, areaTitle: "Organisation" });

const modulesAll = `using Dewiride.Erp.BuildingBlocks.Modules;
using ErpModules = Dewiride.Erp.Modules;

namespace Dewiride.Erp.Host.Composition;

public static class Modules
{
    public static IReadOnlyList<IModule> All { get; } =
    [
        new ErpModules.Platform.SystemInfo.SystemInfoModule(),
        new ErpModules.Platform.Jobs.JobsModule(),
        new ErpModules.Identity.Users.UsersModule(),
    ];
}
`;

const oneLineFixture = `export interface RegisteredNavigation { readonly area: string; readonly title: string; readonly href: string; readonly description: string; readonly employees: boolean; }

export const registeredNavigation: readonly RegisteredNavigation[] = [
  { area: "Platform", title: "System", href: "/platform/system-info", description: "See which version runs.", employees: false },
  { area: "Platform", title: "Attachments", href: "/platform/attachments", description: "Upload files.", employees: true },
  { area: "Organisation", title: "People", href: "/identity/users", description: "Register people.", employees: false },
];
`;

const multiLineFixture = `export const registeredNavigation: readonly RegisteredNavigation[] = [
  {
    area: "Platform",
    title: "System",
    href: "/platform/system-info",
    description: "See which version runs, and its \\"starts\\".",
    employees: false,
  },
  {
    area: "Organisation",
    title: "People",
    href: "/identity/users",
    description: "Register people.",
    employees: false,
  },
];
`;

function real(path: string): string {
  return readFileSync(join(repoRoot, path), "utf8");
}

// An edit only adds lines: every line of the original is still there, in order, and the added lines are returned.
function addedLines(original: string, edited: string): string[] {
  const before = original.split("\n");
  const added: string[] = [];
  let kept = 0;
  for (const line of edited.split("\n")) {
    if (kept < before.length && line === before[kept]) kept += 1;
    else added.push(line);
  }

  assert.equal(kept, before.length, "the edit changed or removed a line it should have kept");
  return added;
}

function assertSorted(values: readonly string[]): void {
  assert.deepEqual(values, [...values].sort(ordinal));
}

function windowsShaped(text: string): string {
  return `\uFEFF${text.replaceAll("\n", "\r\n")}`;
}

function assertKeepsShape(original: string, edit: (text: string, module: ScaffoldModule) => string): void {
  const edited = edit(windowsShaped(original), newDomain);
  assert.equal(edited, windowsShaped(edit(original, newDomain)));
  assert.equal(/(?<!\r)\n/.test(edited), false, "a line ending lost its carriage return");
}

test("a module is described by the names every registration derives from its four names", () => {
  const proof = describeModule({ ...input, domain: "ScaffoldProof", module: "Jobs", entity: "LedgerNote", entities: "LedgerNotes" }, { newDomain: true, areaTitle: "Scaffold proof" });
  assert.equal(proof.featureFlag, "Erp.Modules.ScaffoldProof.Jobs");
  assert.equal(proof.schema, "scaffold_proof_jobs");
  assert.equal(proof.routePrefix, "/scaffold-proof/jobs");
  assert.equal(proof.contextKey, "scaffold-proof-jobs");
  assert.equal(proof.changeKey, "scaffold-proof.jobs.ledger-notes");
  assert.equal(proof.readPermission, "scaffold-proof.jobs.ledger-notes.read");
  assert.equal(proof.createPermission, "scaffold-proof.jobs.ledger-notes.create");
  assert.equal(proof.moduleClass, "ErpModules.ScaffoldProof.Jobs.JobsModule");
  assert.equal(proof.navigationExport, "ledgerNotesNavigation");
  assert.equal(proof.navigationId, "scaffold-proof-jobs-ledger-notes");
  assert.equal(proof.basePath, "/scaffold-proof/jobs/ledger-notes");
  assert.equal(proof.webModule, "@/features/scaffold-proof/jobs");
  assert.deepEqual(proof.apiModels, ["LedgerNoteResponse", "CreateLedgerNoteRequest", "PagedResponseOfLedgerNoteResponse"]);
  assert.deepEqual(proof.projects, [
    "Modules/ScaffoldProof/Jobs/Contracts/Dewiride.Erp.Modules.ScaffoldProof.Jobs.Contracts/Dewiride.Erp.Modules.ScaffoldProof.Jobs.Contracts.csproj",
    "Modules/ScaffoldProof/Jobs/Module/Dewiride.Erp.Modules.ScaffoldProof.Jobs/Dewiride.Erp.Modules.ScaffoldProof.Jobs.csproj",
    "Modules/ScaffoldProof/Jobs/Tests/UnitTests/Dewiride.Erp.Modules.ScaffoldProof.Jobs.UnitTests/Dewiride.Erp.Modules.ScaffoldProof.Jobs.UnitTests.csproj",
    "Modules/ScaffoldProof/Jobs/Tests/IntegrationTests/Dewiride.Erp.Modules.ScaffoldProof.Jobs.IntegrationTests/Dewiride.Erp.Modules.ScaffoldProof.Jobs.IntegrationTests.csproj",
  ]);
  assert.equal(proof.moduleProject, proof.projects[1]);
});

test("Modules.All gains the module, through the alias, as its last entry, and refuses it a second time", () => {
  const edited = registerInModulesAll(modulesAll, newDomain);
  assert.deepEqual(addedLines(modulesAll, edited), ["        new ErpModules.ScaffoldTest.Ledger.LedgerModule(),"]);
  assert.deepEqual(modulesAllOf(edited).modules.at(-1), { domain: "ScaffoldTest", module: "Ledger" });
  assert.throws(() => registerInModulesAll(edited, newDomain), /already registers the module ScaffoldTest\/Ledger/);
  const sameName = describeModule({ ...input, domain: "ScaffoldTest", module: "Users" }, { newDomain: true, areaTitle: "Scaffold test" });
  assert.deepEqual(addedLines(modulesAll, registerInModulesAll(modulesAll, sameName)), ["        new ErpModules.ScaffoldTest.Users.UsersModule(),"]);
});

test("Modules.All without the alias or without its collection is refused", () => {
  assert.throws(() => registerInModulesAll(modulesAll.replace("using ErpModules = Dewiride.Erp.Modules;\n", ""), newDomain), /does not alias Dewiride\.Erp\.Modules as ErpModules/);
  assert.throws(() => registerInModulesAll(modulesAll.replace("All { get; } =", "Every { get; } ="), newDomain), /holds no Modules\.All collection/);
});

test("Modules.All reads the modules it registers and names the entries written another way", () => {
  const { modules, unreadable } = modulesAllOf(modulesAll.replace("new ErpModules.Platform.Jobs.JobsModule(),", "new JobsModule(),"));
  assert.deepEqual(modules, [
    { domain: "Platform", module: "SystemInfo" },
    { domain: "Identity", module: "Users" },
  ]);
  assert.deepEqual(unreadable, ["new JobsModule()"]);
});

test("the composition project references the module's implementation where its path sorts", () => {
  const original = real(paths.composition);
  for (const module of [newDomain, platformModule]) {
    const edited = registerInComposition(original, module);
    assert.deepEqual(addedLines(original, edited), [`    <ProjectReference Include="../../../${module.moduleProject}" />`]);
    assertSorted(projectReferencesOf(edited));
    assert.throws(() => registerInComposition(edited, module), /already references/);
  }

  assert.throws(() => registerInComposition('<Project Sdk="Microsoft.NET.Sdk">\n</Project>\n', platformModule), /holds no <ProjectReference/);
  assertKeepsShape(original, registerInComposition);
});

test("the solution gains the module's two folders where their names sort, each holding its two projects", () => {
  const original = real(paths.solution);
  for (const module of [newDomain, platformModule]) {
    const edited = registerInSolution(original, module);
    const [contracts, implementation, unitTests, integrationTests] = module.projects;
    assert.deepEqual(addedLines(original, edited), [
      `  <Folder Name="/${module.folder}/">`,
      `    <Project Path="${contracts}" />`,
      `    <Project Path="${implementation}" />`,
      "  </Folder>",
      `  <Folder Name="/${module.folder}/Tests/">`,
      `    <Project Path="${unitTests}" />`,
      `    <Project Path="${integrationTests}" />`,
      "  </Folder>",
    ]);
    assertSorted(solutionFoldersOf(edited));
    assert.throws(() => registerInSolution(edited, module), /already lists/);
  }

  assert.throws(() => registerInSolution(original.replace("</Solution>", ""), newDomain), /holds no <\/Solution>/);
  assertKeepsShape(original, registerInSolution);
});

test("a domain's filter gains the module's four projects in ordinal order with backslashes, and a new domain gets a filter of its own", () => {
  const original = real(`${paths.filters}/Platform.slnf`);
  const edited = registerInDomainFilter(original, platformModule);
  const added = platformModule.projects.map((path) => path.replaceAll("/", "\\"));
  assert.deepEqual(addedLines(original, edited).map((line) => JSON.parse(line.trim().replace(/,$/, "")) as string).sort(ordinal), [...added].sort(ordinal));
  assertSorted(filterProjectsOf(edited));
  assert.throws(() => registerInDomainFilter(edited, platformModule), /already lists/);
  assert.throws(() => registerInDomainFilter(JSON.stringify(JSON.parse(original)), platformModule), /not laid out as JSON\.stringify writes it/);
  assertKeepsShape(original, (text) => registerInDomainFilter(text, platformModule));

  const filter = JSON.parse(domainFilter(real(paths.solution), newDomain)) as { solution: { path: string; projects: string[] } };
  assert.equal(filter.solution.path, "..\\Dewiride.Erp.slnx");
  assert.deepEqual(filter.solution.projects.filter((path) => path.startsWith("Modules\\")).sort(ordinal), newDomain.projects.map((path) => path.replaceAll("/", "\\")).sort(ordinal));
  assert.ok(filter.solution.projects.includes("Hosts\\Composition\\Dewiride.Erp.Host.Composition\\Dewiride.Erp.Host.Composition.csproj"));
  assertSorted(filter.solution.projects);
});

test("the filters' README gains a new domain's row where its name sorts", () => {
  const original = real(paths.filtersReadme);
  const edited = registerFilterInReadme(original, newDomain);
  assert.deepEqual(addedLines(original, edited), ["| `ScaffoldTest.slnf` | building blocks, hosts, shared tests and the ScaffoldTest modules |"]);
  assertSorted(filterNamesOf(edited));
  const first = registerFilterInReadme(original, describeModule({ ...input, domain: "AaScaffold" }, { newDomain: true, areaTitle: "Scaffold test" }));
  assert.equal(filterNamesOf(first)[0], "AaScaffold");
  assert.throws(() => registerFilterInReadme(edited, newDomain), /already lists ScaffoldTest\.slnf/);
  assert.throws(() => registerFilterInReadme("# Solution filters\n", newDomain), /holds no \| Filter \| Contents \| table/);
});

test("the flag is seeded enabled under both labels after the last module flag, and nothing else in the file moves", () => {
  const original = real(paths.flags);
  const edited = registerFlag(original, newDomain);
  const seeds = flagSeedsOf(edited);
  const position = seeds.findIndex(({ id }) => id === newDomain.featureFlag);
  assert.deepEqual(seeds[position], { id: "Erp.Modules.ScaffoldTest.Ledger", enabled: { "local-dev": true, production: true } });
  assert.ok(seeds.slice(0, position).every(({ id }) => id.startsWith("Erp.Modules.")));
  assert.ok(seeds.slice(position + 1).every(({ id }) => !id.startsWith("Erp.Modules.")));
  assert.equal(addedLines(original, edited).length, 7);
  assert.throws(() => registerFlag(edited, newDomain), /already seeds Erp\.Modules\.ScaffoldTest\.Ledger/);
  assert.throws(() => registerFlag('{"flags":[]}\n', newDomain), /not laid out as JSON\.stringify writes it/);
  assertKeepsShape(original, registerFlag);
});

test("the configuration register describes the flag after the last module flag", () => {
  const original = real(paths.configuration);
  const edited = registerFlagRow(original, newDomain);
  const [row] = addedLines(original, edited);
  assert.match(row as string, /^\| `Erp\.Modules\.ScaffoldTest\.Ledger` \| module flag of the ScaffoldTest \/ Ledger module: gates the three routes under `\/api\/scaffold-test\/ledger\/scaffold-notes`/);
  const lines = edited.split("\n");
  assert.match(lines[lines.indexOf(row as string) - 1] as string, /^\| `Erp\.Modules\./);
  assert.doesNotMatch(lines[lines.indexOf(row as string) + 1] as string, /^\| `Erp\.Modules\./);
  assert.throws(() => registerFlagRow(edited, newDomain), /already describes Erp\.Modules\.ScaffoldTest\.Ledger/);
  assert.throws(() => registerFlagRow("# Configuration\n", newDomain), /holds no feature flag row/);
  assertKeepsShape(original, registerFlagRow);
});

test("the routing register gains the module's row at the end of the Modules table and its route table before the gating rule", () => {
  const original = real(paths.routing);
  const edited = registerRoutes(original, newDomain);
  const added = addedLines(original, edited);
  assert.match(added[0] as string, /^\| ScaffoldTest \/ Ledger \| `GET \/api\/scaffold-test\/ledger\/scaffold-notes`/);
  assert.equal(added[1], "ScaffoldTest / Ledger routes (every scaffold note is `{ id, name }`):");
  assert.equal(added.at(-1), "");
  assert.equal(routedModulesOf(edited).rows.at(-1), "ScaffoldTest/Ledger");
  assert.equal(routedModulesOf(edited).routeTables.at(-1), "ScaffoldTest/Ledger");
  assert.match(edited, /\| `POST \/api\/scaffold-test\/ledger\/scaffold-notes` \| `ScaffoldTest\.Ledger\.CreateScaffoldNote` \| `scaffold-test\.ledger\.scaffold-notes\.create` \|/);
  assert.match(edited, /`\{ id, name \}`\):\n\n\| Route[^]*\n\nEvery module route group is gated/);
  assert.throws(() => registerRoutes(edited, newDomain), /already lists ScaffoldTest \/ Ledger/);
  assert.throws(() => registerRoutes(original.replace("Every module route group is gated", "Each module route group is gated"), newDomain), /holds no paragraph starting "Every module route group is gated/);
  assertKeepsShape(original, registerRoutes);
});

test("the web registry imports the module's navigation in specifier order and lists it after its domain's last entry", () => {
  const original = real(paths.registry);
  const edited = registerInRegistry(original, platformModule);
  assert.deepEqual(addedLines(original, edited), ['import { scaffoldNotesNavigation } from "@/features/platform/scaffold-ledger";', "  scaffoldNotesNavigation,"]);
  const entries = registryOf(edited);
  const position = entries.findIndex(({ name }) => name === "scaffoldNotesNavigation");
  assert.equal(entries[position]?.from, "@/features/platform/scaffold-ledger");
  assert.ok(entries[position - 1]?.from.startsWith("@/features/platform/"));
  assert.ok(!entries[position + 1]?.from.startsWith("@/features/platform/"));
  assertSorted([...new Set(importsOf(edited).values())]);

  const another = registerInRegistry(original, newDomain);
  assert.equal(registryOf(another).at(-1)?.name, "scaffoldNotesNavigation");
  assertSorted([...new Set(importsOf(another).values())]);
  assert.throws(() => registerInRegistry(edited, platformModule), /already imports @\/features\/platform\/scaffold-ledger/);
  assert.throws(() => registerInRegistry(original.replace("export const navigation", "export const entries"), platformModule), /holds no navigation list/);
  assertKeepsShape(original, registerInRegistry);
});

test("the e2e navigation list gains the entry after its area's last entry, or last for a new area, in the layout of its neighbours", () => {
  const oneLine = registerInNavigationFixture(oneLineFixture, platformModule);
  assert.deepEqual(addedLines(oneLineFixture, oneLine), [
    '  { area: "Platform", title: "Scaffold notes", href: "/platform/scaffold-ledger/scaffold-notes", description: "Keep short notes about the scaffold.", employees: false },',
  ]);
  assert.deepEqual(
    registeredNavigationOf(oneLine).map(({ title }) => title),
    ["System", "Attachments", "Scaffold notes", "People"],
  );

  const multiLine = registerInNavigationFixture(multiLineFixture, newDomain);
  assert.deepEqual(addedLines(multiLineFixture, multiLine), [
    "  {",
    '    area: "Scaffold test",',
    '    title: "Scaffold notes",',
    '    href: "/scaffold-test/ledger/scaffold-notes",',
    '    description: "Keep short notes about the scaffold.",',
    "    employees: false,",
    "  },",
  ]);
  assert.deepEqual(registeredNavigationOf(multiLine)[0], {
    area: "Platform",
    title: "System",
    href: "/platform/system-info",
    description: 'See which version runs, and its "starts".',
    employees: false,
  });
  assert.throws(() => registerInNavigationFixture(multiLine, newDomain), /already lists \/scaffold-test\/ledger\/scaffold-notes/);
  assert.throws(() => registerInNavigationFixture("export const navigation = [];\n", newDomain), /holds no registeredNavigation list/);
  assertKeepsShape(multiLineFixture, registerInNavigationFixture);
  const realFixture = real(paths.navigationFixture);
  assert.deepEqual(registeredNavigationOf(registerInNavigationFixture(realFixture, newDomain)).at(-1), {
    area: "Scaffold test",
    title: "Scaffold notes",
    href: "/scaffold-test/ledger/scaffold-notes",
    description: "Keep short notes about the scaffold.",
    employees: false,
  });
});

test("the audit trail can filter by the module's change key and names its records, after its domain's keys", () => {
  const keysFile = real(paths.changeKeys);
  const withKey = registerChangeKey(keysFile, identityModule);
  const keys = changeKeysOf(withKey);
  const position = keys.indexOf("identity.scaffold-teams.scaffold-teams");
  assert.ok(keys[position - 1]?.startsWith("identity."));
  assert.ok(!keys[position + 1]?.startsWith("identity."));
  assert.equal(changeKeysOf(registerChangeKey(keysFile, newDomain)).at(-1), "scaffold-test.ledger.scaffold-notes");
  assert.throws(() => registerChangeKey(withKey, identityModule), /already lists identity\.scaffold-teams\.scaffold-teams/);

  const labelsFile = real(paths.recordTypes);
  const labelled = registerRecordType(labelsFile, identityModule);
  assert.deepEqual(addedLines(labelsFile, labelled), ['  ["identity.scaffold-teams.scaffold-teams", { label: "Scaffold team", noun: "scaffold team" }],']);
  const labelledKeys = recordTypeKeysOf(labelled);
  assert.ok(labelledKeys[labelledKeys.indexOf("identity.scaffold-teams.scaffold-teams") - 1]?.startsWith("identity."));
  const prose = describeModule({ ...input, singular: "note on a ledger" }, { newDomain: true, areaTitle: "Scaffold test" });
  assert.deepEqual(addedLines(labelsFile, registerRecordType(labelsFile, prose)), ['  ["scaffold-test.ledger.scaffold-notes", { label: "Note on a ledger", noun: "note on a ledger" }],']);
  assert.throws(() => registerRecordType(labelled, identityModule), /already names identity\.scaffold-teams\.scaffold-teams/);
  assertKeepsShape(keysFile, registerChangeKey);
  assertKeepsShape(labelsFile, registerRecordType);
});

test("a file mixing CRLF and LF line endings is refused rather than given a third kind", () => {
  assert.throws(() => preservingShape("a\r\nb\nc\n", (text) => text), /mix CRLF and LF/);
  assert.equal(preservingShape("\uFEFFa\r\nb\r\n", (text) => `${text}c\n`), "\uFEFFa\r\nb\r\nc\r\n");
});

test("a list's elements are read whole, whatever strings, comments and brackets they hold", () => {
  const text = 'const x = [\n  "a, ]", // a comment, with ]\n  { b: [1, 2], c: "}" },\n  /* d */ e\n];\n';
  const list = listLiteral(text, text.indexOf("["));
  assert.deepEqual(
    list.elements.map(({ start, end }) => text.slice(start, end)),
    ['"a, ]",', '{ b: [1, 2], c: "}" },', "e"],
  );
  assert.equal(text[list.close], "]");
  assert.equal(insertElement(text, list, list.elements.at(-1), "f"), 'const x = [\n  "a, ]", // a comment, with ]\n  { b: [1, 2], c: "}" },\n  /* d */ e,\n  f,\n];\n');
  const commented = "const y = [\n  a,\n  b // the last\n];\n";
  const commentedList = listLiteral(commented, commented.indexOf("["));
  assert.equal(insertElement(commented, commentedList, commentedList.elements.at(-1), "c"), "const y = [\n  a,\n  b, // the last\n  c,\n];\n");
});

test("an element is added to an empty list or a list written on one line with every element on a line of its own", () => {
  const empty = "const x = [];\n";
  assert.equal(insertElement(empty, listLiteral(empty, empty.indexOf("[")), undefined, "a"), "const x = [\n  a,\n];\n");
  const open = "const x = [\n];\n";
  assert.equal(insertElement(open, listLiteral(open, open.indexOf("[")), undefined, "a"), "const x = [\n  a,\n];\n");
  const oneLine = "  const x = [a, b];\n";
  const list = listLiteral(oneLine, oneLine.indexOf("["));
  assert.equal(insertElement(oneLine, list, list.elements[0], "c"), "  const x = [\n    a,\n    c,\n    b,\n  ];\n");
  const crowded = "const x = [\n  a, b,\n];\n";
  const crowdedList = listLiteral(crowded, crowded.indexOf("["));
  assert.throws(() => insertElement(crowded, crowdedList, crowdedList.elements[0], "c"), /does not hold one entry per line/);
  assert.throws(() => listLiteral("const x = [a, b", 10), /never closes/);
});
