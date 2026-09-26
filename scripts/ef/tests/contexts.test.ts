import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { test } from "node:test";

import { contextKey, discoverContexts } from "../lib/contexts.ts";

function withTree(files: Record<string, string>, check: (backend: string) => void): void {
  const backend = mkdtempSync(join(tmpdir(), "ef-contexts-"));
  try {
    for (const [path, content] of Object.entries(files)) {
      mkdirSync(dirname(join(backend, path)), { recursive: true });
      writeFileSync(join(backend, path), content);
    }
    check(backend);
  } finally {
    rmSync(backend, { recursive: true, force: true });
  }
}

const context = (name: string) => `namespace X;\n\ninternal sealed class ${name}(DbContextOptions<${name}> options) : ModuleDbContext(options, "x_y")\n{\n}\n`;

test("module and building-block contexts are found with the project that owns their Persistence folder", () => {
  withTree(
    {
      "Modules/Finance/Sales/Module/Dewiride.Erp.Modules.Finance.Sales/Dewiride.Erp.Modules.Finance.Sales.csproj": "<Project />",
      "Modules/Finance/Sales/Module/Dewiride.Erp.Modules.Finance.Sales/Persistence/SalesDbContext.cs": context("SalesDbContext"),
      "BuildingBlocks/Files/Dewiride.Erp.BuildingBlocks.Files/Dewiride.Erp.BuildingBlocks.Files.csproj": "<Project />",
      "BuildingBlocks/Files/Dewiride.Erp.BuildingBlocks.Files/Persistence/FileStoreDbContext.cs": context("FileStoreDbContext"),
    },
    (backend) => {
      assert.deepEqual(discoverContexts(backend), [
        { key: "file-store", contextName: "FileStoreDbContext", project: "BuildingBlocks/Files/Dewiride.Erp.BuildingBlocks.Files" },
        { key: "sales", contextName: "SalesDbContext", project: "Modules/Finance/Sales/Module/Dewiride.Erp.Modules.Finance.Sales" },
      ]);
    },
  );
});

test("test projects, build output, files outside Persistence and classes that are not module contexts are ignored", () => {
  withTree(
    {
      "Modules/Finance/Sales/Module/M/M.csproj": "<Project />",
      "Modules/Finance/Sales/Module/M/Persistence/SalesDbContext.cs": context("SalesDbContext"),
      "Modules/Finance/Sales/Module/M/Persistence/ReportingDbContext.cs": "internal sealed class ReportingDbContext : DbContext { }",
      "Modules/Finance/Sales/Module/M/Shared/StrayDbContext.cs": context("StrayDbContext"),
      "Modules/Finance/Sales/Module/M/obj/Persistence/GeneratedDbContext.cs": context("GeneratedDbContext"),
      "Modules/Finance/Sales/Tests/IntegrationTests/T/T.csproj": "<Project />",
      "Modules/Finance/Sales/Tests/IntegrationTests/T/Persistence/FakeDbContext.cs": context("FakeDbContext"),
    },
    (backend) => {
      assert.deepEqual(discoverContexts(backend).map((entry) => entry.key), ["sales"]);
    },
  );
});

test("two contexts with the same key are reported", () => {
  withTree(
    {
      "Modules/A/One/Module/A/A.csproj": "<Project />",
      "Modules/A/One/Module/A/Persistence/LedgerDbContext.cs": context("LedgerDbContext"),
      "Modules/B/Two/Module/B/B.csproj": "<Project />",
      "Modules/B/Two/Module/B/Persistence/LedgerDbContext.cs": context("LedgerDbContext"),
    },
    (backend) => {
      assert.throws(() => discoverContexts(backend), /two DbContexts resolve to the key "ledger"/);
    },
  );
});

test("a context file without an owning project is reported", () => {
  withTree({ "Modules/A/One/Persistence/OrphanDbContext.cs": context("OrphanDbContext") }, (backend) => {
    assert.throws(() => discoverContexts(backend), /no project file owns/);
  });
});

test("keys are the context name without DbContext in kebab case", () => {
  assert.equal(contextKey("SystemInfoDbContext"), "system-info");
  assert.equal(contextKey("IdempotencyDbContext"), "idempotency");
  assert.equal(contextKey("GstR1DbContext"), "gst-r1");
});

test("the repository's own contexts are discovered", () => {
  const keys = discoverContexts().map((entry) => entry.key);

  assert.ok(keys.includes("system-info"));
  assert.ok(keys.includes("idempotency"));
});
