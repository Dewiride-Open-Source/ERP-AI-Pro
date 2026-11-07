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
