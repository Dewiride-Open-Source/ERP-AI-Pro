import assert from "node:assert/strict";
import { execFile } from "node:child_process";
import { cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { describe, test } from "node:test";
import { fileURLToPath } from "node:url";

import { registrationAudit } from "../lib/audit.ts";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");

const systemInfo = "Modules/Platform/SystemInfo";
const systemInfoAssembly = "Dewiride.Erp.Modules.Platform.SystemInfo";
const shared = [
  "BuildingBlocks/Persistence/Dewiride.Erp.BuildingBlocks.Persistence/Dewiride.Erp.BuildingBlocks.Persistence.csproj",
  "Hosts/Composition/Dewiride.Erp.Host.Composition/Dewiride.Erp.Host.Composition.csproj",
  "Tests/Architecture/Dewiride.Erp.ArchitectureTests/Dewiride.Erp.ArchitectureTests.csproj",
];
const systemInfoProjects = [
  `${systemInfo}/Contracts/${systemInfoAssembly}.Contracts/${systemInfoAssembly}.Contracts.csproj`,
  `${systemInfo}/Module/${systemInfoAssembly}/${systemInfoAssembly}.csproj`,
  `${systemInfo}/Tests/UnitTests/${systemInfoAssembly}.UnitTests/${systemInfoAssembly}.UnitTests.csproj`,
  `${systemInfo}/Tests/IntegrationTests/${systemInfoAssembly}.IntegrationTests/${systemInfoAssembly}.IntegrationTests.csproj`,
];

const repository: Record<string, string> = {
  ".gitignore": "node_modules/\n",
  "backend/Hosts/Composition/Dewiride.Erp.Host.Composition/Modules.cs": `using Dewiride.Erp.BuildingBlocks.Modules;
using ErpModules = Dewiride.Erp.Modules;

namespace Dewiride.Erp.Host.Composition;

public static class Modules
{
    public static IReadOnlyList<IModule> All { get; } =
    [
        new ErpModules.Platform.SystemInfo.SystemInfoModule(),
    ];
}
`,
  "backend/Hosts/Composition/Dewiride.Erp.Host.Composition/Dewiride.Erp.Host.Composition.csproj": `<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="../../../${shared[0]}" />
    <ProjectReference Include="../../../${systemInfoProjects[1]}" />
  </ItemGroup>

</Project>
`,
  "backend/Hosts/Composition/Dewiride.Erp.Host.Composition/packages.lock.json": '{\n  "version": 2\n}\n',
  "backend/Dewiride.Erp.slnx": `<Solution>
  <Folder Name="/BuildingBlocks/">
    <Project Path="${shared[0]}" />
  </Folder>
  <Folder Name="/Hosts/">
    <Project Path="${shared[1]}" />
  </Folder>
  <Folder Name="/${systemInfo}/">
    <Project Path="${systemInfoProjects[0]}" />
    <Project Path="${systemInfoProjects[1]}" />
  </Folder>
  <Folder Name="/${systemInfo}/Tests/">
    <Project Path="${systemInfoProjects[2]}" />
    <Project Path="${systemInfoProjects[3]}" />
  </Folder>
  <Folder Name="/Tests/">
    <Project Path="${shared[2]}" />
  </Folder>
</Solution>
`,
  "backend/solutions/Platform.slnf": `${JSON.stringify(
    { solution: { path: "..\\Dewiride.Erp.slnx", projects: [...shared, ...systemInfoProjects].map((path) => path.replaceAll("/", "\\")).sort((left, right) => (left < right ? -1 : 1)) } },
    null,
    2,
  )}\n`,
  "backend/solutions/README.md": "# Solution filters\n\n| Filter | Contents |\n|---|---|\n| `Platform.slnf` | building blocks, hosts, shared tests and the Platform modules |\n",
  [`backend/${systemInfoProjects[1]}`]: "<Project />\n",
  [`backend/${systemInfo}/Module/${systemInfoAssembly}/SystemInfoModule.cs`]: `namespace ${systemInfoAssembly};

public sealed class SystemInfoModule : IModule
{
    public const string FeatureFlag = "Erp.Modules.Platform.SystemInfo";

    public ModuleDescriptor Descriptor { get; } = new(
        Domain: "Platform",
        Name: "SystemInfo",
        Schema: null,
        RoutePrefix: "/platform/system-info",
        FeatureFlag: FeatureFlag,
        Permissions:
        [
            new(SystemInfoPermissions.ReadInfo, [], "See which version of the ERP is running."),
        ],
        Capabilities: []);
}
`,
  [`backend/${systemInfo}/Contracts/${systemInfoAssembly}.Contracts/SystemInfoPermissions.cs`]: `namespace ${systemInfoAssembly}.Contracts;

public static class SystemInfoPermissions
{
    public const string ReadInfo = "platform.system-info.info.read";
}
`,
  "backend/BuildingBlocks/Attachments/Dewiride.Erp.BuildingBlocks.Attachments/Persistence/AttachmentConfiguration.cs": '        builder.RecordChangesAs("platform.attachments.files");\n',
  "infra/appconfig/feature-flags.json": `${JSON.stringify(
    {
      flags: [
        { id: "Erp.Modules.Platform.SystemInfo", enabled: { "local-dev": true, production: true } },
        { id: "Erp.Platform.Identity.BearerTokens", enabled: { "local-dev": false, production: false } },
      ],
    },
    null,
    2,
  )}\n`,
  "docs/configuration.md": "# Configuration register\n\n## Feature flags\n\n| Flag | Purpose |\n|---|---|\n| `Erp.Modules.Platform.SystemInfo` | module flag of the system-info module |\n| `Erp.Platform.Identity.BearerTokens` | platform flag |\n\nEvaluation follows the catalog.\n",
  "docs/routing.md": `# Routing register

## Modules

| Domain / Module | API group | Web routes | Notes |
|---|---|---|---|
| Platform / SystemInfo | \`GET /api/platform/system-info\` | \`/platform/system-info\` | administrators alone |

Platform / SystemInfo routes (every answer is \`{ version }\`):

| Route | Name | Permission | Answers |
|---|---|---|---|
| \`GET /api/platform/system-info\` | \`Platform.SystemInfo.Get\` | \`platform.system-info.info.read\` | \`200\` |

Every module route group is gated by its module flag.

## Web-only routes
`,
  "docs/openapi/erp.json": '{\n  "paths": {\n    "/api/platform/system-info": {}\n  },\n  "components": {\n    "schemas": {\n      "SystemInfoResponse": {}\n    }\n  }\n}\n',
  "frontend/packages/api-client/src/generated/index.ts": "export {};\n",
  "frontend/apps/web/src/features/registry.ts": `import { systemInfoNavigation } from "@/features/platform/system-info";
import type { NavigationEntry } from "@/shared/layout/navigation-entry";

export const navigation: readonly NavigationEntry[] = [systemInfoNavigation];
`,
  "frontend/apps/web/src/features/platform/system-info/index.ts": 'export { systemInfoNavigation } from "./nav";\n',
  "frontend/apps/web/src/features/platform/system-info/nav.ts": `import { InfoIcon } from "lucide-react";

import { platformArea } from "../_shared/platform-area";

export const systemInfoNavigation = {
  id: "platform-system-info",
  title: "System",
  description: "See which version of the ERP is running.",
  basePath: "/platform/system-info",
  featureFlag: "Erp.Modules.Platform.SystemInfo",
  permission: "platform.system-info.info.read",
  area: platformArea,
  icon: InfoIcon,
} as const;
`,
  "frontend/apps/web/src/features/platform/_shared/platform-area.ts": 'export const platformArea = { id: "platform", title: "Platform" } as const;\n',
  "frontend/apps/web/src/features/platform/audit/changes/lists/changes.list.ts": 'export const changeKeys = ["platform.attachments.files"] as const;\n',
  "frontend/apps/web/src/features/platform/audit/changes/components/change-text.ts": `const recordTypes = new Map<string, RecordType>([
  ["platform.attachments.files", { label: "File", noun: "file", createdVerb: "uploaded" }],
]);
`,
  "frontend/apps/web/node_modules/lucide-react/package.json": '{ "name": "lucide-react", "types": "dist/lucide-react.d.ts" }\n',
  "frontend/apps/web/node_modules/lucide-react/dist/lucide-react.d.ts": "export { Info, Info as InfoIcon, Notebook, Notebook as NotebookIcon, FileText as FileTextIcon };\n",
  "frontend/e2e/fixtures/navigation.ts": `export const registeredNavigation: readonly RegisteredNavigation[] = [
  {
    area: "Platform",
    title: "System",
    href: "/platform/system-info",
    description: "See which version of the ERP is running.",
    employees: false,
  },
];
`,
};

const templates: Record<string, string> = {
  "backend/README.md.tmpl": "# __Domain__ / __Module__\n",
  "backend/Module/Dewiride.Erp.Modules.__Domain__.__Module__/Dewiride.Erp.Modules.__Domain__.__Module__.csproj.tmpl": "<Project />\n",
  "backend/Module/Dewiride.Erp.Modules.__Domain__.__Module__/__Module__Module.cs.tmpl": `namespace Dewiride.Erp.Modules.__Domain__.__Module__;

public sealed class __Module__Module : IModule
{
    public const string FeatureFlag = "Erp.Modules.__Domain__.__Module__";

    public ModuleDescriptor Descriptor { get; } = new(
        Domain: "__Domain__",
        Name: "__Module__",
        Schema: __Module__DbContext.SchemaName,
        RoutePrefix: "/__domain-kebab__/__module-kebab__",
        FeatureFlag: FeatureFlag,
        Permissions:
        [
            new(__Module__Permissions.Read__Entities__, [], "__ReadSentence__"),
            new(__Module__Permissions.Create__Entities__, [], "__CreateSentence__"),
        ],
        Capabilities: []);
}
`,
  "backend/Module/Dewiride.Erp.Modules.__Domain__.__Module__/Persistence/__Module__DbContext.cs.tmpl": `namespace Dewiride.Erp.Modules.__Domain__.__Module__.Persistence;

internal sealed class __Module__DbContext(DbContextOptions<__Module__DbContext> options) : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = "__domain_snake_____module_snake__";
}
`,
  "backend/Module/Dewiride.Erp.Modules.__Domain__.__Module__/__Entities__/Persistence/__Entity__Configuration.cs.tmpl": '        builder.RecordChangesAs("__domain-kebab__.__module-kebab__.__entities-kebab__");\n',
  "backend/Contracts/Dewiride.Erp.Modules.__Domain__.__Module__.Contracts/__Module__Permissions.cs.tmpl": `namespace Dewiride.Erp.Modules.__Domain__.__Module__.Contracts;

public static class __Module__Permissions
{
    public const string Read__Entities__ = "__domain-kebab__.__module-kebab__.__entities-kebab__.read";

    public const string Create__Entities__ = "__domain-kebab__.__module-kebab__.__entities-kebab__.create";
}
`,
  "web/feature/index.ts.tmpl": 'export { __entities__Navigation } from "./nav";\n',
  "web/feature/nav.ts.tmpl": `import { __Icon__ } from "lucide-react";

import { __domain__Area } from "../_shared/__domain-kebab__-area";

export const __entities__Navigation = {
  id: "__domain-kebab__-__module-kebab__-__entities-kebab__",
  title: "__Title__",
  description: "__Description__",
  basePath: "/__domain-kebab__/__module-kebab__/__entities-kebab__",
  featureFlag: "Erp.Modules.__Domain__.__Module__",
  permission: "__domain-kebab__.__module-kebab__.__entities-kebab__.read",
  area: __domain__Area,
  icon: __Icon__,
} as const;
`,
  "web/routes/layout.tsx.tmpl": "export default function Layout() {}\n",
  "web/area/__domain-kebab__-area.ts.tmpl": 'export const __domain__Area = { id: "__domain-kebab__", title: "__AreaTitle__" } as const;\n',
  "e2e/tests/__domain-kebab__/__module-kebab__/__entities-kebab__.spec.ts.tmpl": 'test("__Entities-words__", () => {});\n',
};

const newDomainArguments = [
  "ScaffoldProof/Jobs",
  "--entity",
  "LedgerNote",
  "--title",
  "Ledger notes",
  "--description",
  "Keep short notes about the ledgers.",
  "--read-permission",
  "See the ledger notes.",
  "--create-permission",
  "Add ledger notes.",
  "--area-title",
  "Scaffold proof",
  "--icon",
  "NotebookIcon",
];

type Outcome = { readonly status: number; readonly stdout: string; readonly stderr: string };

function run(file: string, args: readonly string[], cwd: string, environment: NodeJS.ProcessEnv = process.env): Promise<Outcome> {
  return new Promise((settle) => {
    execFile(file, [...args], { cwd, env: environment, encoding: "utf8", shell: false, maxBuffer: 16 * 1024 * 1024 }, (error, stdout, stderr) => {
      settle({ status: error === null ? 0 : typeof error.code === "number" ? error.code : 1, stdout, stderr });
    });
  });
}

async function git(root: string, ...args: string[]): Promise<string> {
  const outcome = await run("git", args, root);
  assert.equal(outcome.status, 0, `git ${args.join(" ")}: ${outcome.stderr}`);
  return outcome.stdout;
}

async function commit(root: string): Promise<void> {
  await git(root, "add", "--all");
  await git(root, "-c", "user.name=Scaffold test", "-c", "user.email=scaffold@example.invalid", "commit", "--quiet", "--no-gpg-sign", "--allow-empty", "--message", "fixture");
}

function write(root: string, files: Record<string, string>): void {
  for (const [path, text] of Object.entries(files)) {
    mkdirSync(dirname(join(root, path)), { recursive: true });
    writeFileSync(join(root, path), text);
  }
}

// A throwaway repository holding one module, the scaffolder's own scripts and snippets, and small templates in place of the
// real ones, committed, so a test sees what one run changes and git can undo it.
function withRepository(check: (root: string) => Promise<void>, change?: (root: string) => void): () => Promise<void> {
  return async () => {
    const root = mkdtempSync(join(tmpdir(), "scaffold-cli-"));
    try {
      for (const path of [
        "scripts/scaffold/new-module.ts",
        "scripts/scaffold/lib",
        "scripts/scaffold/templates/registrations",
        "scripts/ef/lib/contexts.ts",
        "scripts/checks/lib/comment-policy.ts",
        "scripts/checks/lib/code-text.ts",
      ]) {
        cpSync(join(repoRoot, path), join(root, path), { recursive: true });
      }

      write(root, repository);
      write(root, Object.fromEntries(Object.entries(templates).map(([path, text]) => [`scripts/scaffold/templates/${path}`, text])));
      await git(root, "init", "--quiet");
      await git(root, "config", "core.autocrlf", "false");
      change?.(root);
      await commit(root);
      await check(root);
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  };
}

function scaffold(root: string, args: readonly string[], environment: NodeJS.ProcessEnv = process.env): Promise<Outcome> {
  return run(process.execPath, [join(root, "scripts", "scaffold", "new-module.ts"), ...args], root, environment);
}

async function changedFiles(root: string): Promise<string[]> {
  return (await git(root, "status", "--porcelain=v1", "--untracked-files=all"))
    .split("\n")
    .filter((line) => line !== "")
    .map((line) => line.slice(3))
    .sort();
}

function argumentsOf(line: string): string[] {
  return [...line.matchAll(/'([^']*)'|(\S+)/g)].map((match) => (match[1] ?? match[2]) as string);
}

// Each test owns a repository of its own, so the tests run side by side.
describe("the new-module command", { concurrency: true }, () => {
  test(
    "a module of a new domain is written and registered everywhere, the audit finds nothing missing, and the printed lines undo it all",
    withRepository(async (root) => {
      assert.deepEqual(registrationAudit(root), []);
      const run = await scaffold(root, [...newDomainArguments, "--files-only"]);
      assert.equal(run.status, 0, run.stderr);
      assert.deepEqual(registrationAudit(root), []);
      assert.deepEqual(await changedFiles(root), [
        "backend/Dewiride.Erp.slnx",
        "backend/Hosts/Composition/Dewiride.Erp.Host.Composition/Dewiride.Erp.Host.Composition.csproj",
        "backend/Hosts/Composition/Dewiride.Erp.Host.Composition/Modules.cs",
        "backend/Modules/ScaffoldProof/Jobs/Contracts/Dewiride.Erp.Modules.ScaffoldProof.Jobs.Contracts/JobsPermissions.cs",
        "backend/Modules/ScaffoldProof/Jobs/Module/Dewiride.Erp.Modules.ScaffoldProof.Jobs/Dewiride.Erp.Modules.ScaffoldProof.Jobs.csproj",
        "backend/Modules/ScaffoldProof/Jobs/Module/Dewiride.Erp.Modules.ScaffoldProof.Jobs/JobsModule.cs",
        "backend/Modules/ScaffoldProof/Jobs/Module/Dewiride.Erp.Modules.ScaffoldProof.Jobs/LedgerNotes/Persistence/LedgerNoteConfiguration.cs",
        "backend/Modules/ScaffoldProof/Jobs/Module/Dewiride.Erp.Modules.ScaffoldProof.Jobs/Persistence/JobsDbContext.cs",
        "backend/Modules/ScaffoldProof/Jobs/README.md",
        "backend/solutions/README.md",
        "backend/solutions/ScaffoldProof.slnf",
        "docs/configuration.md",
        "docs/routing.md",
        "frontend/apps/web/src/app/(app)/scaffold-proof/jobs/layout.tsx",
        "frontend/apps/web/src/features/platform/audit/changes/components/change-text.ts",
        "frontend/apps/web/src/features/platform/audit/changes/lists/changes.list.ts",
        "frontend/apps/web/src/features/registry.ts",
        "frontend/apps/web/src/features/scaffold-proof/_shared/scaffold-proof-area.ts",
        "frontend/apps/web/src/features/scaffold-proof/jobs/index.ts",
        "frontend/apps/web/src/features/scaffold-proof/jobs/nav.ts",
        "frontend/e2e/fixtures/navigation.ts",
        "frontend/e2e/tests/scaffold-proof/jobs/ledger-notes.spec.ts",
        "infra/appconfig/feature-flags.json",
      ]);
      assert.match(run.stdout, /Finish the module from the repository root with:\n {2}cd backend\n {2}dotnet tool restore\n/);
      assert.match(run.stdout, /node scripts\/ef\/ef\.ts add InitialLedgerNotes --context scaffold-proof-jobs/);
      assert.match(run.stdout, /bash scripts\/azure\/seed\.sh --label all/);

      for (const line of run.stdout.split("\n").filter((text) => /^ {2}git (restore|clean) /.test(text))) {
        const [, ...args] = argumentsOf(line.trim());
        await git(root, ...args);
      }

      assert.deepEqual(await changedFiles(root), []);
    }),
  );

  test(
    "a module of an existing domain joins its domain's filter, area and audit labels",
    withRepository(async (root) => {
      const args = ["Platform/Ledger", ...newDomainArguments.slice(1, -4), "--files-only"];
      const run = await scaffold(root, args);
      assert.equal(run.status, 0, run.stderr);
      assert.deepEqual(registrationAudit(root), []);
      assert.equal(existsSync(join(root, "frontend/apps/web/src/features/platform/ledger/nav.ts")), true);
      assert.match(readFileSync(join(root, "backend/solutions/Platform.slnf"), "utf8"), /Modules\\\\Platform\\\\Ledger\\\\Module/);
      assert.match(readFileSync(join(root, "frontend/e2e/fixtures/navigation.ts"), "utf8"), /area: "Platform",\n {4}title: "Ledger notes"/);
    }),
  );

  test(
    "a second run of the same module is refused and changes nothing",
    withRepository(async (root) => {
      assert.equal((await scaffold(root, [...newDomainArguments, "--files-only"])).status, 0);
      await commit(root);
      const identical = await scaffold(root, [...newDomainArguments, "--files-only"]);
      assert.equal(identical.status, 1);
      assert.match(identical.stderr, /the domain ScaffoldProof exists and its area keeps its title, so leave out --area-title/);
      const repeat = await scaffold(root, [...newDomainArguments.slice(0, -4), ...newDomainArguments.slice(-2), "--files-only"]);
      assert.equal(repeat.status, 1);
      assert.match(repeat.stderr, /nothing was written/);
      assert.match(repeat.stderr, /backend\/Modules\/ScaffoldProof\/Jobs already exists/);
      assert.match(repeat.stderr, /Modules\.cs: it already registers the module ScaffoldProof\/Jobs/);
      assert.match(repeat.stderr, /the migration key scaffold-proof-jobs already belongs to JobsDbContext/);
      assert.deepEqual(await changedFiles(root), []);
    }),
  );

  test(
    "a run with invalid names, or names another part of the ERP uses, lists every problem and writes nothing",
    withRepository(async (root) => {
      const invalid = await scaffold(root, ["scaffold-proof/Jobs", "--entity", "LedgerNote", "--entities", "LedgerNote", "--title", "Ledger notes", "--description", "No full stop", "--read-permission", "See notes.", "--create-permission", "Add notes.", "--files-only"]);
      assert.equal(invalid.status, 1);
      assert.match(invalid.stderr, /the domain "scaffold-proof" is not PascalCase/);
      assert.match(invalid.stderr, /the entity and its plural are both "LedgerNote"/);
      assert.match(invalid.stderr, /the description "No full stop" is not one sentence/);

      const taken = await scaffold(root, ["Platform/Ledger", ...newDomainArguments.slice(1, 3), "--title", "System", ...newDomainArguments.slice(5, 11), "--area-title", "Platform", "--icon", "MissingIcon", "--files-only"]);
      assert.equal(taken.status, 1);
      assert.match(taken.stderr, /the domain Platform exists and its area keeps its title, so leave out --area-title/);
      assert.match(taken.stderr, /the navigation title "System" is already used/);
      assert.match(taken.stderr, /lucide-react exports no icon named MissingIcon/);
      assert.deepEqual(await changedFiles(root), []);

      const usage = await scaffold(root, ["ScaffoldProof/Jobs", "--files-only"]);
      assert.equal(usage.status, 1);
      assert.match(usage.stderr, /--entity is required/);
      assert.match(usage.stderr, /--read-permission is required/);
      assert.match(usage.stderr, /Usage: node scripts\/scaffold\/new-module\.ts/);
    }),
  );

  test(
    "a malformed name is reported together with the clashes of the well-formed names and texts, the uncommitted targets and the missing settings, and nothing is written",
    withRepository(async (root) => {
      writeFileSync(join(root, "docs/routing.md"), `${readFileSync(join(root, "docs/routing.md"), "utf8")}\nA note being written.\n`);
      const environment = { ...process.env };
      delete environment.ERP_TEST_SQL_CONNECTION;
      const run = await scaffold(root, ["ScaffoldProof/Lock", "--entity", "ledgerNote", "--entities", "LedgerNotes", "--title", "System", ...newDomainArguments.slice(5)], environment);
      assert.equal(run.status, 1);
      assert.match(run.stderr, /the entity "ledgerNote" is not PascalCase/);
      assert.match(run.stderr, /the module "Lock" makes the namespace Dewiride\.Erp\.Modules\.ScaffoldProof\.Lock, which hides the type System\.Threading\.Lock/);
      assert.match(run.stderr, /the navigation title "System" is already used/);
      assert.match(run.stderr, /docs\/routing\.md has uncommitted changes/);
      assert.match(run.stderr, /ERP_TEST_SQL_CONNECTION is not set/);
      assert.deepEqual(await changedFiles(root), ["docs/routing.md"]);
    }),
  );

  test(
    "a module whose files git would ignore is refused, naming the rule, so no commit can leave them out",
    withRepository(
      async (root) => {
        const run = await scaffold(root, [...newDomainArguments.slice(0, 1), "--entity", "TestResult", ...newDomainArguments.slice(3), "--files-only"]);
        assert.equal(run.status, 1);
        assert.match(run.stderr, /TestResults\/Persistence\/TestResultConfiguration\.cs matches the git ignore rule TestResults\/ \(\.gitignore:1\), so a commit would leave it out/);
        assert.deepEqual(await changedFiles(root), []);
      },
      (root) => writeFileSync(join(root, ".gitignore"), "TestResults/\n"),
    ),
  );

  test(
    "a target with uncommitted changes is refused, so git restore and git clean always undo a run",
    withRepository(async (root) => {
      writeFileSync(join(root, "docs/routing.md"), `${readFileSync(join(root, "docs/routing.md"), "utf8")}\nA note being written.\n`);
      const run = await scaffold(root, [...newDomainArguments, "--files-only"]);
      assert.equal(run.status, 1);
      assert.match(run.stderr, /docs\/routing\.md has uncommitted changes/);
      assert.deepEqual(await changedFiles(root), ["docs/routing.md"]);
    }),
  );

  test(
    "a target whose anchor is missing refuses the whole run",
    withRepository(
      async (root) => {
        const run = await scaffold(root, [...newDomainArguments, "--files-only"]);
        assert.equal(run.status, 1);
        assert.match(run.stderr, /docs\/routing\.md: it holds no paragraph starting "Every module route group is gated by its module flag"/);
        assert.deepEqual(await changedFiles(root), []);
      },
      (root) => writeFileSync(join(root, "docs/routing.md"), readFileSync(join(root, "docs/routing.md"), "utf8").replace("Every module route group is gated by its module flag.\n\n", "")),
    ),
  );

  test(
    "without --files-only, a run that could not finish the module is refused before it writes anything",
    withRepository(async (root) => {
      const environment = { ...process.env };
      delete environment.ERP_TEST_SQL_CONNECTION;
      delete environment.ERP_TEST_BLOB_EMULATOR_HOST;
      const run = await scaffold(root, newDomainArguments, environment);
      assert.equal(run.status, 1);
      assert.match(run.stderr, /ERP_TEST_SQL_CONNECTION is not set/);
      assert.match(run.stderr, /ERP_TEST_BLOB_EMULATOR_HOST is not set/);
      assert.match(run.stderr, /frontend\/node_modules\/prettier\/bin\/prettier\.cjs is missing/);
      assert.deepEqual(await changedFiles(root), []);
    }),
  );

  test(
    "the audit names every registration a module lacks and every registration no module owns",
    withRepository(async (root) => {
      const edit = (path: string, change: (text: string) => string): void => writeFileSync(join(root, path), change(readFileSync(join(root, path), "utf8")));
      edit("docs/configuration.md", (text) => text.replace("| `Erp.Modules.Platform.SystemInfo` | module flag of the system-info module |\n", ""));
      edit("infra/appconfig/feature-flags.json", (text) => text.replace('"Erp.Platform.Identity.BearerTokens"', '"Erp.Modules.Platform.Ghost"'));
      edit("frontend/e2e/fixtures/navigation.ts", (text) => text.replace("employees: false", "employees: true"));
      edit("frontend/apps/web/src/features/platform/_shared/platform-area.ts", (text) => text.replace("platformArea", "platformSection"));
      edit("frontend/apps/web/src/features/platform/audit/changes/lists/changes.list.ts", (text) => text.replace('"platform.attachments.files"', '"platform.attachments.blobs"'));
      edit("backend/solutions/Platform.slnf", (text) => text.replace(/\n {6}"Tests\\\\Architecture[^\n]*/, "").replace(/,\n {4}\]/, "\n    ]"));
      const problems = registrationAudit(root);
      assert.deepEqual(problems, [
        "backend/solutions/Platform.slnf does not list Tests\\Architecture\\Dewiride.Erp.ArchitectureTests\\Dewiride.Erp.ArchitectureTests.csproj",
        "docs/configuration.md has no row for Erp.Modules.Platform.SystemInfo",
        "infra/appconfig/feature-flags.json seeds Erp.Modules.Platform.Ghost, which belongs to no module of Modules.All",
        "frontend/apps/web/src/features/platform/_shared/platform-area.ts does not export platformArea = { id, title } as const",
        "frontend/apps/web/src/features/platform/system-info/nav.ts: systemInfoNavigation names the area platformArea, which no area file exports",
        'frontend/e2e/fixtures/navigation.ts: registeredNavigation[0] is {"area":"Platform","title":"System","href":"/platform/system-info","description":"See which version of the ERP is running.","employees":true}, but the registry\'s entry 0 is {"area":"","title":"System","href":"/platform/system-info","description":"See which version of the ERP is running.","employees":false}',
        "frontend/apps/web/src/features/platform/audit/changes/lists/changes.list.ts: changeKeys does not list platform.attachments.files, so the audit trail cannot filter by it",
        "frontend/apps/web/src/features/platform/audit/changes/lists/changes.list.ts: changeKeys lists platform.attachments.blobs, which no entity records",
      ]);
    }),
  );
});
