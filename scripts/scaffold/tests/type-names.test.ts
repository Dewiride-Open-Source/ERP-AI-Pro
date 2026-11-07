import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";

import type { ScaffoldInput } from "../lib/names.ts";
import {
  dotnetTypeNamesFile,
  entityNamespacesOf,
  entityScopeMemberNamesOf,
  implicitNamespaces,
  repositoryTypeNames,
  testNamespaces,
  typeNameProblems,
  typeNamespacesOf,
} from "../lib/type-names.ts";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
const templates = join(repoRoot, "scripts", "scaffold", "templates");

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

test("the committed .NET type names cover exactly the namespaces the templates that name the entity import, sorted, each name once", () => {
  const document = JSON.parse(readFileSync(dotnetTypeNamesFile, "utf8")) as Record<string, string[]>;
  assert.deepEqual(Object.keys(document), typeNamespacesOf(templates), "run node scripts/scaffold/type-names.ts after the templates import another namespace");
  for (const [namespace, names] of Object.entries(document)) {
    assert.ok(names.length > 0, namespace);
    assert.deepEqual(names, [...new Set(names)].sort(), namespace);
    for (const name of names) assert.match(name, /^[A-Za-z_][A-Za-z0-9_]*$/, `${namespace}.${name}`);
  }

  assert.ok(document.System?.includes("Environment"));
  assert.ok(document["System.IO"]?.includes("Path"));
  assert.ok(document["System.Threading"]?.includes("Lock"));
  assert.ok(document.Xunit?.includes("Record"));
  assert.ok(document["Microsoft.EntityFrameworkCore"]?.includes("DbContext"));
  assert.equal(document.System?.includes("Lazy"), false, "a type that is only generic is not a clash");
});

test("the namespaces of the entity come from the templates that name it on its own, with the implicit ones and Xunit for the tests", () => {
  const { external, repository } = entityNamespacesOf(templates);
  for (const namespace of [...implicitNamespaces, ...testNamespaces, "Microsoft.EntityFrameworkCore"]) assert.ok(external.includes(namespace), namespace);
  assert.ok(repository.includes("Dewiride.Erp.BuildingBlocks.Kernel.Results"));
  assert.equal([...external, ...repository].some((namespace) => namespace.includes("__")), false);
});

test("the building blocks' types are read from their sources, public, at the top of a file and not generic", () => {
  const types = repositoryTypeNames(repoRoot, ["Dewiride.Erp.BuildingBlocks.Kernel.Results", "Dewiride.Erp.BuildingBlocks.Kernel.Domain"]);
  assert.deepEqual([...(types.get("Dewiride.Erp.BuildingBlocks.Kernel.Results") ?? [])].sort(), ["Error", "ErrorKind", "Result"]);
  assert.equal(types.get("Dewiride.Erp.BuildingBlocks.Kernel.Domain")?.has("AggregateRoot"), false);
  assert.equal(types.get("Dewiride.Erp.BuildingBlocks.Kernel.Domain")?.has("IAuditable"), true);
});

test("the members and positional record parameters of every generated type whose file names the entity are read from the templates", () => {
  const members = entityScopeMemberNamesOf(templates);
  for (const member of ["Create", "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy", "Name", "NameMaxLength", "Field", "NotFound", "Configure", "HandleAsync", "Id"]) {
    assert.ok(members.includes(member), member);
  }

  assert.equal(members.includes("LedgerNote"), false);
  assert.equal(members.some((member) => member.includes("__")), false);
});

test("an entity, domain, module or plural the compiler would misread is refused, naming the type it meets, and a plain name is not", () => {
  const problemsOf = (input: ScaffoldInput) => typeNameProblems(repoRoot, input, templates).join("\n");
  assert.deepEqual(typeNameProblems(repoRoot, valid, templates), []);
  assert.match(problemsOf({ ...valid, entity: "Environment", entities: "Environments" }), /the entity "Environment" is also the type System\.Environment/);
  assert.match(problemsOf({ ...valid, entity: "Record", entities: "Records" }), /the entity "Record" is also the type Xunit\.Record/);
  assert.match(problemsOf({ ...valid, entity: "PageRequest", entities: "PageRequests" }), /the entity "PageRequest" is also the type Dewiride\.Erp\.BuildingBlocks\.Application\.Queries\.Paging\.PageRequest/);
  assert.match(problemsOf({ ...valid, entity: "Name", entities: "Names" }), /the entity "Name" is also a member of a generated type that names the entity/);
  assert.match(problemsOf({ ...valid, entity: "Field", entities: "Fields" }), /the entity "Field" is also a member of a generated type that names the entity/);
  assert.match(problemsOf({ ...valid, domain: "Environment" }), /the domain "Environment" makes the namespace Dewiride\.Erp\.Modules\.Environment, which hides the type System\.Environment/);
  assert.match(problemsOf({ ...valid, module: "Lock" }), /the module "Lock" makes the namespace Dewiride\.Erp\.Modules\.ScaffoldProof\.Lock, which hides the type System\.Threading\.Lock/);
  assert.match(problemsOf({ ...valid, entities: "Path" }), /the entities "Path" makes the namespace Dewiride\.Erp\.Modules\.ScaffoldProof\.Jobs\.Path, which hides the type System\.IO\.Path/);
  assert.deepEqual(typeNameProblems(repoRoot, { ...valid, entity: "Route", entities: "Routes" }, templates), [], "a type the files naming the entity do not import is no clash");
});
