import assert from "node:assert/strict";
import { test } from "node:test";

import { addArguments, pendingArguments, scriptArguments, startupProject, updateArguments } from "../lib/commands.ts";
import type { DbContextEntry } from "../lib/contexts.ts";

const sales: DbContextEntry = { key: "sales", contextName: "SalesDbContext", project: "Modules/Finance/Sales/Module/Dewiride.Erp.Modules.Finance.Sales" };

const target = ["--context", "SalesDbContext", "--project", "Modules/Finance/Sales/Module/Dewiride.Erp.Modules.Finance.Sales", "--startup-project", startupProject];

test("add writes the migration into the context project's Persistence/Migrations folder", () => {
  assert.deepEqual(addArguments(sales, "AddInvoices"), ["ef", "migrations", "add", "AddInvoices", ...target, "--output-dir", "Persistence/Migrations"]);
});

test("update targets the context through the API host and passes the optional migration, connection and build switches", () => {
  assert.deepEqual(updateArguments(sales), ["ef", "database", "update", ...target]);
  assert.deepEqual(updateArguments(sales, { migration: "AddInvoices", connection: "Server=db", noBuild: true, configuration: "Release" }), [
    "ef",
    "database",
    "update",
    "AddInvoices",
    ...target,
    "--connection",
    "Server=db",
    "--no-build",
    "--configuration",
    "Release",
  ]);
});

test("pending checks the model against the last migration", () => {
  assert.deepEqual(pendingArguments(sales, { noBuild: true }), ["ef", "migrations", "has-pending-model-changes", ...target, "--no-build"]);
});

test("script writes the SQL between two migrations, starting from the empty database when only the end is given", () => {
  assert.deepEqual(scriptArguments(sales), ["ef", "migrations", "script", ...target]);
  assert.deepEqual(scriptArguments(sales, { to: "AddInvoices" }), ["ef", "migrations", "script", "0", "AddInvoices", ...target]);
  assert.deepEqual(scriptArguments(sales, { from: "Initial", to: "AddInvoices", idempotent: true, output: "out.sql" }), [
    "ef",
    "migrations",
    "script",
    "Initial",
    "AddInvoices",
    ...target,
    "--idempotent",
    "--output",
    "out.sql",
  ]);
});

test("the API host is the startup project", () => {
  assert.equal(startupProject, "Hosts/Api/Dewiride.Erp.Host.Api");
});
