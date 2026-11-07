import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { test } from "node:test";

import { collisionProblems, domainStateOf } from "../lib/collisions.ts";
import type { ScaffoldInput } from "../lib/names.ts";
import { describeModule } from "../lib/registrations/module.ts";

const repository: Record<string, string> = {
  "backend/Modules/Platform/SystemInfo/Module/Dewiride.Erp.Modules.Platform.SystemInfo/Dewiride.Erp.Modules.Platform.SystemInfo.csproj": "<Project />\n",
  "backend/Modules/Platform/SystemInfo/Module/Dewiride.Erp.Modules.Platform.SystemInfo/SystemInfoModule.cs": `namespace Dewiride.Erp.Modules.Platform.SystemInfo;

public sealed class SystemInfoModule : IModule
{
    public const string FeatureFlag = "Erp.Modules.Platform.SystemInfo";

    public ModuleDescriptor Descriptor { get; } = new(
        Domain: "Platform",
        Name: "SystemInfo",
        Schema: SystemInfoDbContext.SchemaName,
        RoutePrefix: "/platform/system-info",
        FeatureFlag: FeatureFlag,
        Permissions:
        [
            new(SystemInfoPermissions.ReadInfo, [], "See which version of the ERP is running."),
        ],
        Capabilities: []);
}
`,
  "backend/Modules/Platform/SystemInfo/Module/Dewiride.Erp.Modules.Platform.SystemInfo/Persistence/SystemInfoDbContext.cs":
    'namespace Dewiride.Erp.Modules.Platform.SystemInfo.Persistence;\n\ninternal sealed class SystemInfoDbContext(DbContextOptions<SystemInfoDbContext> options) : ModuleDbContext(options, SchemaName)\n{\n    public const string SchemaName = "platform_system_info";\n}\n',
  "backend/Modules/Platform/SystemInfo/Contracts/Dewiride.Erp.Modules.Platform.SystemInfo.Contracts/SystemInfoPermissions.cs":
    'namespace Dewiride.Erp.Modules.Platform.SystemInfo.Contracts;\n\npublic static class SystemInfoPermissions\n{\n    public const string ReadInfo = "platform.system-info.info.read";\n}\n',
  "backend/Modules/Identity/Users/Module/Dewiride.Erp.Modules.Identity.Users/UsersModule.cs": 'public sealed class UsersModule\n{\n    public const string FeatureFlag = "Erp.Modules.Identity.Users";\n}\n',
  "backend/BuildingBlocks/Jobs/Dewiride.Erp.BuildingBlocks.Jobs/Dewiride.Erp.BuildingBlocks.Jobs.csproj": "<Project />\n",
  "backend/BuildingBlocks/Jobs/Dewiride.Erp.BuildingBlocks.Jobs/Persistence/JobsDbContext.cs":
    'namespace Dewiride.Erp.BuildingBlocks.Jobs.Persistence;\n\ninternal sealed class JobsDbContext(DbContextOptions<JobsDbContext> options) : ModuleDbContext(options, SchemaName)\n{\n    public const string SchemaName = "platform_jobs";\n}\n',
  "backend/BuildingBlocks/Files/Dewiride.Erp.BuildingBlocks.Files/Dewiride.Erp.BuildingBlocks.Files.csproj": "<Project />\n",
  "backend/BuildingBlocks/Files/Dewiride.Erp.BuildingBlocks.Files/Persistence/FileStoreDbContext.cs":
    'namespace Dewiride.Erp.BuildingBlocks.Files.Persistence;\n\ninternal sealed class FileStoreDbContext(DbContextOptions<FileStoreDbContext> options) : ModuleDbContext(options, "files")\n{\n}\n',
  "backend/BuildingBlocks/Files/Dewiride.Erp.BuildingBlocks.Files/Persistence/Migrations/20260101000000_Initial.cs": 'migrationBuilder.EnsureSchema(name: "files_archive");\n',
  "infra/appconfig/feature-flags.json": '{\n  "flags": [\n    { "id": "Erp.Modules.Platform.SystemInfo", "enabled": { "local-dev": true, "production": true } },\n    { "id": "Erp.Modules.Platform.Archive.Search", "enabled": { "local-dev": false, "production": false } }\n  ]\n}\n',
  "docs/openapi/erp.json": JSON.stringify({ paths: { "/api/platform/system-info": {}, "/api/platform/features": {} }, components: { schemas: { DesignationResponse: {}, StartupResponse: {} } } }),
  "frontend/apps/web/src/features/platform/_shared/platform-area.ts": 'export const platformArea = { id: "platform", title: "Platform" } as const;\n',
  "frontend/apps/web/src/features/identity/_shared/identity-area.ts": 'export const identityArea = { id: "identity", title: "Organisation" } as const;\n',
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
  "frontend/apps/web/src/features/platform/design/index.ts": "export {};\n",
  "frontend/apps/web/src/shared/layout/navigation/navigation-trail.ts": 'export const homeTitle = "Home";\n',
  "frontend/apps/web/node_modules/lucide-react/package.json": '{ "name": "lucide-react", "types": "dist/lucide-react.d.ts" }\n',
  "frontend/apps/web/node_modules/lucide-react/dist/lucide-react.d.ts": "export { Info, Info as InfoIcon, Notebook, Notebook as NotebookIcon };\nexport type { LucideIcon };\n",
  "frontend/e2e/tests/shared/layout/navigation.spec.ts": "export {};\n",
};

const ledger: ScaffoldInput = {
  domain: "Finance",
  module: "Ledger",
  entity: "LedgerNote",
  entities: "LedgerNotes",
  title: "Ledger notes",
  description: "Keep short notes about the ledgers.",
  readSentence: "See the ledger notes.",
  createSentence: "Add ledger notes.",
  areaTitle: "Finance",
  icon: "NotebookIcon",
};

function withRepository(check: (root: string) => void, extra: Record<string, string> = {}, removed: readonly string[] = []): () => void {
  return () => {
    const root = mkdtempSync(join(tmpdir(), "scaffold-collisions-"));
    try {
      for (const [path, text] of Object.entries({ ...repository, ...extra })) {
        if (removed.includes(path)) continue;
        mkdirSync(dirname(join(root, path)), { recursive: true });
        writeFileSync(join(root, path), text);
      }

      check(root);
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  };
}

function problemsOf(root: string, input: ScaffoldInput): string[] {
  const domain = domainStateOf(root, input);
  return [...domain.problems, ...collisionProblems(root, describeModule(input, domain))];
}

test(
  "a module whose every name is free has no problems, in a new domain or an existing one",
  withRepository((root) => {
    assert.deepEqual(problemsOf(root, ledger), []);
    assert.deepEqual(problemsOf(root, { ...ledger, domain: "Platform", areaTitle: undefined }), []);
  }),
);

test(
  "an existing domain keeps its area's title, and the area title is required for a new domain and refused for an existing one",
  withRepository((root) => {
    assert.deepEqual(domainStateOf(root, { ...ledger, domain: "Platform", areaTitle: undefined }), { newDomain: false, areaTitle: "Platform", problems: [] });
    assert.deepEqual(domainStateOf(root, ledger), { newDomain: true, areaTitle: "Finance", problems: [] });
    assert.match(domainStateOf(root, { ...ledger, areaTitle: undefined }).problems.join("\n"), /the domain Finance is new, so --area-title must give its area's title/);
    assert.match(domainStateOf(root, { ...ledger, domain: "Platform" }).problems.join("\n"), /the domain Platform exists and its area keeps its title, so leave out --area-title/);
    assert.match(domainStateOf(root, { ...ledger, domain: "PlatForm" }).problems.join("\n"), /the domain PlatForm differs from the domain Platform only in letter case/);
  }),
);

test(
  "an existing domain without its navigation area is refused",
  withRepository(
    (root) => {
      assert.match(domainStateOf(root, { ...ledger, domain: "Platform", areaTitle: undefined }).problems.join("\n"), /features\/platform\/_shared\/platform-area\.ts does not export platformArea/);
    },
    {},
    ["frontend/apps/web/src/features/platform/_shared/platform-area.ts"],
  ),
);

test(
  "a schema, migration key, route, flag or API model another part of the ERP uses is refused, each named with where it is used",
  withRepository((root) => {
    const platform = { ...ledger, domain: "Platform", areaTitle: undefined };
    assert.match(problemsOf(root, { ...platform, module: "Jobs" }).join("\n"), /the schema platform_jobs is already used \(backend\/BuildingBlocks\/Jobs\/.*JobsDbContext\.cs\)/);
    assert.match(problemsOf(root, { ...ledger, domain: "Files", module: "Archive", areaTitle: "Files" }).join("\n"), /the schema files_archive is already used \(.*Migrations\/20260101000000_Initial\.cs\)/);
    assert.match(problemsOf(root, { ...ledger, domain: "File", module: "Store", areaTitle: "Store" }).join("\n"), /the migration key file-store already belongs to FileStoreDbContext/);
    assert.match(problemsOf(root, { ...platform, module: "Features" }).join("\n"), /the API already answers \/api\/platform\/features/);
    assert.match(problemsOf(root, { ...platform, module: "Archive" }).join("\n"), /infra\/appconfig\/feature-flags\.json already seeds Erp\.Modules\.Platform\.Archive\.Search/);
    assert.match(problemsOf(root, { ...ledger, domain: "Identity", module: "Users", areaTitle: undefined }).join("\n"), /the feature flag Erp\.Modules\.Identity\.Users is already declared/);
    const models = problemsOf(root, { ...ledger, entity: "Designation", entities: "Designations" }).join("\n");
    assert.match(models, /the API already has a model named DesignationResponse/);
  }),
);

test(
  "a permission sentence or navigation title already in use, and an icon lucide-react lacks, are refused",
  withRepository((root) => {
    const problems = problemsOf(root, { ...ledger, readSentence: "see which version of the ERP is RUNNING.", createSentence: "See which version of the ERP is running.", title: "system", icon: "MissingIcon" }).join("\n");
    assert.match(problems, /the read and create permissions are described by the same sentence/);
    assert.match(problems, /the permission platform\.system-info\.info\.read already says "See which version of the ERP is running\."/);
    assert.match(problems, /the navigation title "System" is already used \(frontend\/apps\/web\/src\/features\/platform\/system-info\/nav\.ts\)/);
    assert.match(problems, /lucide-react exports no icon named MissingIcon/);
    assert.match(problemsOf(root, { ...ledger, title: "Home" }).join("\n"), /the navigation title "Home" is the shell's own Home link/);
    assert.match(problemsOf(root, { ...ledger, areaTitle: "platform" }).join("\n"), /the area title "Platform" is already used/);
    assert.match(problemsOf(root, { ...ledger, entity: "System", entities: "SystemInfo" }).join("\n"), /the navigation export systemInfoNavigation already exists/);
  }),
);

test(
  "a missing lucide-react package is a problem, never a skipped check",
  withRepository(
    (root) => {
      assert.match(problemsOf(root, ledger).join("\n"), /frontend\/apps\/web\/node_modules\/lucide-react is missing, so the icon cannot be checked/);
    },
    {},
    ["frontend/apps/web/node_modules/lucide-react/package.json"],
  ),
);

test(
  "a folder the module would write into that already exists is refused, also a folder of a module that differs only in letter case",
  withRepository((root) => {
    const platform = { ...ledger, domain: "Platform", areaTitle: undefined };
    assert.match(problemsOf(root, { ...platform, module: "SystemInfo" }).join("\n"), /backend\/Modules\/Platform\/SystemInfo already exists/);
    assert.match(problemsOf(root, { ...platform, module: "Systeminfo" }).join("\n"), /the module Systeminfo differs from the module Platform\/SystemInfo only in letter case/);
    assert.match(problemsOf(root, { ...platform, module: "Design" }).join("\n"), /frontend\/apps\/web\/src\/features\/platform\/design already exists/);
    assert.match(problemsOf(root, { ...ledger, domain: "Shared", areaTitle: "Shared" }).join("\n"), /frontend\/e2e\/tests\/shared already exists/);
  }),
);
