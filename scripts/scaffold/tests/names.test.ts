import assert from "node:assert/strict";
import { test } from "node:test";

import { formsOf, nameProblems, type ScaffoldInput, tokensOf } from "../lib/names.ts";

const valid: ScaffoldInput = {
  domain: "ScaffoldProof",
  module: "Jobs",
  entity: "LedgerNote",
  entities: "LedgerNotes",
  title: "Ledger notes",
  description: "Keep short notes about ledgers.",
  readSentence: "See the ledger notes.",
  createSentence: "Add ledger notes.",
  areaTitle: "Scaffold proof",
  icon: "NotebookIcon",
};

test("a name splits into words where a lower-case letter or digit meets a capital, as the module catalogue's naming rule does", () => {
  assert.deepEqual(formsOf("SystemInfo"), { pascal: "SystemInfo", camel: "systemInfo", kebab: "system-info", snake: "system_info", words: "system info" });
  assert.deepEqual(formsOf("Gstr1Returns"), { pascal: "Gstr1Returns", camel: "gstr1Returns", kebab: "gstr1-returns", snake: "gstr1_returns", words: "gstr1 returns" });
  assert.deepEqual(formsOf("Hr"), { pascal: "Hr", camel: "hr", kebab: "hr", snake: "hr", words: "hr" });
});

test("valid input has no problems", () => {
  assert.deepEqual(nameProblems(valid), []);
});

test("every malformed or reserved name is reported, all at once", () => {
  const problems = nameProblems({ ...valid, domain: "Api", module: "SalesAi", entity: "GST", entities: "Results" });
  assert.equal(problems.length, 4);
  assert.match(problems.join("\n"), /the entity "GST" is not PascalCase/);
  assert.match(problems.join("\n"), /the entities "Results" would shadow a type or namespace/);
  assert.match(problems.join("\n"), /the module "SalesAi" ends in Ai/);
  assert.match(problems.join("\n"), /the domain "Api" would take the route \/api/);
});

test("an entity equal to its plural, prose that is not lower-case words, and texts outside their forms are refused", () => {
  const problems = nameProblems({
    ...valid,
    entities: "LedgerNote",
    plural: "Ledger  notes",
    description: "Keeps notes. And more.",
    readSentence: "see the notes.",
    title: "ledger notes",
    areaTitle: "A".repeat(41),
    icon: "notebook",
  });
  assert.equal(problems.length, 7);
});

test("an entity named as its module, its domain or a generated folder, and a change key beyond 64 characters, are refused", () => {
  const problemsOf = (input: ScaffoldInput) => nameProblems(input).join("\n");
  assert.match(problemsOf({ ...valid, module: "Notes", entity: "Notes", entities: "NoteSets" }), /the entity "Notes" is also the module/);
  assert.match(problemsOf({ ...valid, entity: "ScaffoldProof" }), /the entity "ScaffoldProof" is also the domain/);
  assert.match(problemsOf({ ...valid, entity: "Application", entities: "Applications" }), /the entity "Application" would shadow/);
  assert.match(problemsOf({ ...valid, module: "Contracts" }), /the module "Contracts" would shadow/);
  assert.match(problemsOf({ ...valid, entities: "VeryLongLedgerNotesKeptForTheScaffoldProofOfEveryBranch" }), /longer than the 64 characters/);
});

test("a single-word domain, module or plural that becomes a reserved word of the API client is refused, and a longer name holding one is not", () => {
  const problemsOf = (input: ScaffoldInput) => nameProblems(input).join("\n");
  assert.match(problemsOf({ ...valid, domain: "Default" }), /the domain "Default" becomes the API client property "default"/);
  assert.match(problemsOf({ ...valid, module: "New" }), /the module "New" becomes the API client property "new"/);
  assert.deepEqual(nameProblems({ ...valid, module: "DefaultSettings", entities: "Returns", entity: "Return" }), []);
});

test("a sentence with a character that would break a string literal or a table of the docs is refused, while every sentence the modules use passes", () => {
  for (const sentence of ['Says "hello".', "A back\\slash.", "A | pipe.", "A {brace}.", "A `tick`."]) {
    assert.equal(nameProblems({ ...valid, readSentence: sentence }).length, 1, sentence);
  }

  for (const sentence of ["See the company's departments and which department each one sits under.", "See the work the ERP does on its own, when it runs next, and how each run went.", "Upload and download the files kept with the ERP."]) {
    assert.deepEqual(nameProblems({ ...valid, readSentence: sentence }), [], sentence);
  }
});

test("tokens carry every form of the four names, the prose overrides and the texts", () => {
  const tokens = tokensOf({ ...valid, singular: "note on a ledger", plural: "notes on ledgers" });
  assert.equal(tokens.get("__Domain__"), "ScaffoldProof");
  assert.equal(tokens.get("__domain__"), "scaffoldProof");
  assert.equal(tokens.get("__domain-kebab__"), "scaffold-proof");
  assert.equal(tokens.get("__module_snake__"), "jobs");
  assert.equal(tokens.get("__Entities__"), "LedgerNotes");
  assert.equal(tokens.get("__entities-kebab__"), "ledger-notes");
  assert.equal(tokens.get("__entity-words__"), "note on a ledger");
  assert.equal(tokens.get("__Entities-words__"), "Notes on ledgers");
  assert.equal(tokens.get("__AreaTitle__"), "Scaffold proof");
  assert.equal(tokens.get("__Icon__"), "NotebookIcon");
  assert.equal(tokensOf({ ...valid, areaTitle: undefined }).has("__AreaTitle__"), false);
});
