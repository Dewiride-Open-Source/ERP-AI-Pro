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

const context = (namespace: string, name: string) =>
  `namespace ${namespace};\n\ninternal sealed class ${name}(DbContextOptions<${name}> options) : ModuleDbContext(options, "x_y")\n{\n}\n`;

test("module and building-block contexts are found with their full names and the project that owns their Persistence folder", () => {
  withTree(
    {
      "Modules/Finance/Sales/Module/Dewiride.Erp.Modules.Finance.Sales/Dewiride.Erp.Modules.Finance.Sales.csproj": "<Project />",
      "Modules/Finance/Sales/Module/Dewiride.Erp.Modules.Finance.Sales/Persistence/SalesDbContext.cs": context("Dewiride.Erp.Modules.Finance.Sales.Persistence", "SalesDbContext"),
      "BuildingBlocks/Files/Dewiride.Erp.BuildingBlocks.Files/Dewiride.Erp.BuildingBlocks.Files.csproj": "<Project />",
      "BuildingBlocks/Files/Dewiride.Erp.BuildingBlocks.Files/Persistence/FileStoreDbContext.cs": context("Dewiride.Erp.BuildingBlocks.Files.Persistence", "FileStoreDbContext"),
    },
    (backend) => {
      assert.deepEqual(discoverContexts(backend), [
        {
          key: "file-store",
          contextName: "FileStoreDbContext",
          fullName: "Dewiride.Erp.BuildingBlocks.Files.Persistence.FileStoreDbContext",
          project: "BuildingBlocks/Files/Dewiride.Erp.BuildingBlocks.Files",
        },
        {
          key: "finance-sales",
          contextName: "SalesDbContext",
          fullName: "Dewiride.Erp.Modules.Finance.Sales.Persistence.SalesDbContext",
          project: "Modules/Finance/Sales/Module/Dewiride.Erp.Modules.Finance.Sales",
        },
      ]);
    },
  );
});

test("two modules of one name in different domains get keys and full names of their own", () => {
  withTree(
    {
      "Modules/Clients/Core/Module/Dewiride.Erp.Modules.Clients.Core/Dewiride.Erp.Modules.Clients.Core.csproj": "<Project />",
      "Modules/Clients/Core/Module/Dewiride.Erp.Modules.Clients.Core/Persistence/CoreDbContext.cs": context("Dewiride.Erp.Modules.Clients.Core.Persistence", "CoreDbContext"),
      "Modules/Vendors/Core/Module/Dewiride.Erp.Modules.Vendors.Core/Dewiride.Erp.Modules.Vendors.Core.csproj": "<Project />",
      "Modules/Vendors/Core/Module/Dewiride.Erp.Modules.Vendors.Core/Persistence/CoreDbContext.cs": context("Dewiride.Erp.Modules.Vendors.Core.Persistence", "CoreDbContext"),
    },
    (backend) => {
      assert.deepEqual(
        discoverContexts(backend).map(({ key, fullName }) => ({ key, fullName })),
        [
          { key: "clients-core", fullName: "Dewiride.Erp.Modules.Clients.Core.Persistence.CoreDbContext" },
          { key: "vendors-core", fullName: "Dewiride.Erp.Modules.Vendors.Core.Persistence.CoreDbContext" },
        ],
      );
    },
  );
});

test("a module context and a building block context of one class name get keys and full names of their own", () => {
  withTree(
    {
      "Modules/ScaffoldProof/Jobs/Module/Dewiride.Erp.Modules.ScaffoldProof.Jobs/Dewiride.Erp.Modules.ScaffoldProof.Jobs.csproj": "<Project />",
      "Modules/ScaffoldProof/Jobs/Module/Dewiride.Erp.Modules.ScaffoldProof.Jobs/Persistence/JobsDbContext.cs": context("Dewiride.Erp.Modules.ScaffoldProof.Jobs.Persistence", "JobsDbContext"),
      "BuildingBlocks/Jobs/Dewiride.Erp.BuildingBlocks.Jobs/Dewiride.Erp.BuildingBlocks.Jobs.csproj": "<Project />",
      "BuildingBlocks/Jobs/Dewiride.Erp.BuildingBlocks.Jobs/Persistence/JobsDbContext.cs": context("Dewiride.Erp.BuildingBlocks.Jobs.Persistence", "JobsDbContext"),
    },
    (backend) => {
      assert.deepEqual(
        discoverContexts(backend).map(({ key, fullName }) => ({ key, fullName })),
        [
          { key: "jobs", fullName: "Dewiride.Erp.BuildingBlocks.Jobs.Persistence.JobsDbContext" },
          { key: "scaffold-proof-jobs", fullName: "Dewiride.Erp.Modules.ScaffoldProof.Jobs.Persistence.JobsDbContext" },
        ],
      );
    },
  );
});

test("a namespace declared with a block is read as well as a file-scoped one", () => {
  withTree(
    {
      "BuildingBlocks/Files/F/F.csproj": "<Project />",
      "BuildingBlocks/Files/F/Persistence/FileStoreDbContext.cs":
        "using Microsoft.EntityFrameworkCore;\n\nnamespace Dewiride.Erp.BuildingBlocks.Files.Persistence\n{\n    internal sealed class FileStoreDbContext(DbContextOptions<FileStoreDbContext> options) : ModuleDbContext(options, \"files\")\n    {\n    }\n}\n",
    },
    (backend) => {
      assert.deepEqual(discoverContexts(backend).map((entry) => entry.fullName), ["Dewiride.Erp.BuildingBlocks.Files.Persistence.FileStoreDbContext"]);
    },
  );
});

test("test projects, build output, files outside Persistence and classes that are not module contexts are ignored", () => {
  withTree(
    {
      "Modules/Finance/Sales/Module/M/M.csproj": "<Project />",
      "Modules/Finance/Sales/Module/M/Persistence/SalesDbContext.cs": context("M.Persistence", "SalesDbContext"),
      "Modules/Finance/Sales/Module/M/Persistence/ReportingDbContext.cs": "namespace M.Persistence;\n\ninternal sealed class ReportingDbContext : DbContext { }",
      "Modules/Finance/Sales/Module/M/Shared/StrayDbContext.cs": context("M.Shared", "StrayDbContext"),
      "Modules/Finance/Sales/Module/M/obj/Persistence/GeneratedDbContext.cs": context("M.Persistence", "GeneratedDbContext"),
      "Modules/Finance/Sales/Tests/IntegrationTests/T/T.csproj": "<Project />",
      "Modules/Finance/Sales/Tests/IntegrationTests/T/Persistence/FakeDbContext.cs": context("T.Persistence", "FakeDbContext"),
    },
    (backend) => {
      assert.deepEqual(discoverContexts(backend).map((entry) => entry.key), ["finance-sales"]);
    },
  );
});

test("two contexts of one module, which resolve to the same key, are reported", () => {
  withTree(
    {
      "Modules/Finance/Sales/Module/M/M.csproj": "<Project />",
      "Modules/Finance/Sales/Module/M/Persistence/SalesDbContext.cs": context("M.Persistence", "SalesDbContext"),
      "Modules/Finance/Sales/Module/M/Persistence/LedgerDbContext.cs": context("M.Persistence", "LedgerDbContext"),
    },
    (backend) => {
      assert.throws(() => discoverContexts(backend), /two DbContexts resolve to the key "finance-sales"/);
    },
  );
});

test("a context file without an owning project is reported", () => {
  withTree({ "Modules/A/One/Persistence/OrphanDbContext.cs": context("A.One.Persistence", "OrphanDbContext") }, (backend) => {
    assert.throws(() => discoverContexts(backend), /no project file owns/);
  });
});

test("a context file without a namespace is reported", () => {
  withTree(
    {
      "BuildingBlocks/Files/F/F.csproj": "<Project />",
      "BuildingBlocks/Files/F/Persistence/FileStoreDbContext.cs":
        "internal sealed class FileStoreDbContext(DbContextOptions<FileStoreDbContext> options) : ModuleDbContext(options, \"files\")\n{\n}\n",
    },
    (backend) => {
      assert.throws(() => discoverContexts(backend), /BuildingBlocks\/Files\/F\/Persistence\/FileStoreDbContext\.cs declares no namespace/);
    },
  );
});

test("a module's key is its domain and module in kebab case, and a building block's its class name without DbContext", () => {
  assert.equal(contextKey("Modules/Platform/SystemInfo/Module/Dewiride.Erp.Modules.Platform.SystemInfo/Persistence", "SystemInfoDbContext"), "platform-system-info");
  assert.equal(contextKey("Modules/Finance/GstR1/Module/Dewiride.Erp.Modules.Finance.GstR1/Persistence", "ReturnsDbContext"), "finance-gst-r1");
  assert.equal(contextKey("BuildingBlocks/Idempotency/Dewiride.Erp.BuildingBlocks.Idempotency/Persistence", "IdempotencyDbContext"), "idempotency");
  assert.equal(contextKey("BuildingBlocks/Returns/Dewiride.Erp.BuildingBlocks.Returns/Persistence", "GstR1DbContext"), "gst-r1");
});

test("the repository's own contexts are discovered", () => {
  const contexts = discoverContexts();

  assert.ok(contexts.some((entry) => entry.key === "platform-system-info" && entry.fullName === "Dewiride.Erp.Modules.Platform.SystemInfo.Persistence.SystemInfoDbContext"));
  assert.ok(contexts.some((entry) => entry.key === "identity-users" && entry.fullName === "Dewiride.Erp.Modules.Identity.Users.Persistence.UsersDbContext"));
  assert.ok(contexts.some((entry) => entry.key === "idempotency" && entry.fullName === "Dewiride.Erp.BuildingBlocks.Idempotency.Persistence.IdempotencyDbContext"));
});
