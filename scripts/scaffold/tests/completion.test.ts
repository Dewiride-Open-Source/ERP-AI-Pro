import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";

import { commandLines, type CompletionStep, completionProblems, completionSteps, quoted, runCompletion } from "../lib/completion.ts";
import { describeModule } from "../lib/registrations/module.ts";

const module = describeModule(
  {
    domain: "ScaffoldProof",
    module: "Jobs",
    entity: "LedgerNote",
    entities: "LedgerNotes",
    title: "Ledger notes",
    description: "Keep short notes about the ledgers.",
    readSentence: "See the ledger notes.",
    createSentence: "Add ledger notes.",
    areaTitle: "Scaffold proof",
    icon: "NotebookIcon",
  },
  { newDomain: true, areaTitle: "Scaffold proof" },
);

const written = [
  "backend/Modules/ScaffoldProof/Jobs/Module/Dewiride.Erp.Modules.ScaffoldProof.Jobs/JobsModule.cs",
  "backend/Modules/ScaffoldProof/Jobs/README.md",
  "backend/Hosts/Composition/Dewiride.Erp.Host.Composition/Modules.cs",
  "backend/Dewiride.Erp.slnx",
  "frontend/apps/web/src/app/(app)/scaffold-proof/jobs/ledger-notes/[ledgerNoteId]/page.tsx",
  "frontend/apps/web/src/features/registry.ts",
  "frontend/e2e/fixtures/navigation.ts",
  "docs/routing.md",
];

test("the completion restores, formats this run's C# files, adds the migration, refreshes the contract and client, then formats this run's web files", () => {
  const steps = completionSteps(module, written);
  assert.deepEqual(
    steps.map(({ program, args, folder }) => [folder, program, ...args]),
    [
      ["backend", "dotnet", "tool", "restore"],
      ["backend", "dotnet", "restore"],
      [
        "backend",
        "dotnet",
        "format",
        "--no-restore",
        "--include",
        "Modules/ScaffoldProof/Jobs/Module/Dewiride.Erp.Modules.ScaffoldProof.Jobs/JobsModule.cs",
        "Hosts/Composition/Dewiride.Erp.Host.Composition/Modules.cs",
      ],
      [".", "node", "scripts/ef/ef.ts", "add", "InitialLedgerNotes", "--context", "scaffold-proof-jobs"],
      [".", "node", "scripts/api-client/generate.ts"],
      [
        "frontend",
        "node",
        "node_modules/prettier/bin/prettier.cjs",
        "--write",
        "apps/web/src/app/(app)/scaffold-proof/jobs/ledger-notes/[ledgerNoteId]/page.tsx",
        "apps/web/src/features/registry.ts",
        "e2e/fixtures/navigation.ts",
      ],
    ],
  );
});

test("a path that could carry anything but a plain file name is never passed to a tool", () => {
  assert.throws(() => completionSteps(module, ["backend/Modules/X/a b.cs"]), /these paths are not passed to a formatter: Modules\/X\/a b\.cs/);
  assert.throws(() => completionSteps(module, ["frontend/apps/../x.ts"]), /apps\/\.\.\/x\.ts/);
  assert.throws(() => completionSteps(module, ["frontend/apps/$(x).ts"]), /not passed to a formatter/);
});

test("a failing step stops the completion, which names it and hands back every step from it on", () => {
  const steps = completionSteps(module, written);
  const ran: string[] = [];
  const outcome = runCompletion(steps, (step) => {
    ran.push(step.name);
    return step.name === "add the first migration" ? 1 : 0;
  });
  assert.equal(outcome.failed?.name, "add the first migration");
  assert.deepEqual(outcome.remaining.map(({ name }) => name), ["add the first migration", "refresh the OpenAPI contract and the web client", "format the web and end-to-end files"]);
  assert.deepEqual(ran.at(-1), "add the first migration");
  assert.deepEqual(runCompletion(steps, () => 0), { failed: undefined, remaining: [] });
});

test("the remaining commands start and end in the repository root and quote what bash or PowerShell would read otherwise", () => {
  const steps: CompletionStep[] = completionSteps(module, written).slice(2);
  assert.deepEqual(commandLines(steps), [
    "cd backend",
    "dotnet format --no-restore --include Modules/ScaffoldProof/Jobs/Module/Dewiride.Erp.Modules.ScaffoldProof.Jobs/JobsModule.cs Hosts/Composition/Dewiride.Erp.Host.Composition/Modules.cs",
    "cd ..",
    "node scripts/ef/ef.ts add InitialLedgerNotes --context scaffold-proof-jobs",
    "node scripts/api-client/generate.ts",
    "cd frontend",
    "node node_modules/prettier/bin/prettier.cjs --write 'apps/web/src/app/(app)/scaffold-proof/jobs/ledger-notes/[ledgerNoteId]/page.tsx' apps/web/src/features/registry.ts e2e/fixtures/navigation.ts",
    "cd ..",
  ]);
  assert.equal(quoted(":(glob)backend/**/packages.lock.json"), "':(glob)backend/**/packages.lock.json'");
  assert.throws(() => quoted("it's"), /holds a single quote/);
});

test("the completion needs the test host's settings, a working dotnet and the frontend's Prettier, each missing one named", () => {
  const root = mkdtempSync(join(tmpdir(), "scaffold-completion-"));
  try {
    const problems = completionProblems(root, {}, () => undefined);
    assert.equal(problems.length, 4);
    assert.match(problems.join("\n"), /ERP_TEST_SQL_CONNECTION is not set/);
    assert.match(problems.join("\n"), /ERP_TEST_BLOB_EMULATOR_HOST is not set/);
    assert.match(problems.join("\n"), /dotnet cannot run in backend/);
    assert.match(problems.join("\n"), /frontend\/node_modules\/prettier\/bin\/prettier\.cjs is missing/);

    mkdirSync(join(root, "frontend", "node_modules", "prettier", "bin"), { recursive: true });
    writeFileSync(join(root, "frontend", "node_modules", "prettier", "bin", "prettier.cjs"), "");
    assert.deepEqual(completionProblems(root, { ERP_TEST_SQL_CONNECTION: "Server=x", ERP_TEST_BLOB_EMULATOR_HOST: "127.0.0.1" }, () => "10.0.100"), []);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
