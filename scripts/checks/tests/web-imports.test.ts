import assert from "node:assert/strict";
import { test } from "node:test";

import { importViolations } from "../lib/web-imports.ts";

type Case = { readonly condition: string; readonly source: string; readonly content: string; readonly expected: readonly string[] };

const systemInfoComponent = "features/platform/system-info/info/components/system-info-overview.tsx";
const attachmentsComponent = "features/platform/attachments/files/components/attachments-overview.tsx";
const invoiceComponent = "features/finance/sales/invoices/components/invoice-table.tsx";
const attachmentsQueries = "@/features/platform/attachments/files/server/queries";

const found = (source: string, content: string): string[] =>
  importViolations([{ path: source, content }])
    .map((violation) => violation.rule)
    .sort();

const cases: readonly Case[] = [
  {
    condition: "StaticImportIntoAnotherModulesInternals",
    source: systemInfoComponent,
    content: `import { listAttachments } from "${attachmentsQueries}";\n`,
    expected: ["I1"],
  },
  {
    condition: "TypeImportIntoAnotherModulesInternals",
    source: systemInfoComponent,
    content: `import type { AttachmentPage } from "${attachmentsQueries}";\n`,
    expected: ["I1"],
  },
  {
    condition: "MultiLineImportIntoAnotherModulesInternals",
    source: systemInfoComponent,
    content: `import {\n  attachmentsPageSize,\n  listAttachments,\n} from "${attachmentsQueries}";\n`,
    expected: ["I1"],
  },
  {
    condition: "ReExportFromAnotherModulesInternals",
    source: "features/platform/system-info/index.ts",
    content: `export { listAttachments } from "${attachmentsQueries}";\n`,
    expected: ["I1"],
  },
  {
    condition: "ExportAllFromAnotherModulesInternals",
    source: "features/platform/system-info/index.ts",
    content: `export * from "${attachmentsQueries}";\n`,
    expected: ["I1"],
  },
  {
    condition: "SideEffectImportOfAnotherModulesInternals",
    source: systemInfoComponent,
    content: `import "${attachmentsQueries}";\n`,
    expected: ["I1"],
  },
  {
    condition: "DynamicImportOfAnotherModulesInternals",
    source: systemInfoComponent,
    content: `const queries = await import("${attachmentsQueries}");\n`,
    expected: ["I1"],
  },
  {
    condition: "TemplateLiteralDynamicImportOfAnotherModulesInternals",
    source: systemInfoComponent,
    content: "const queries = await import(`" + attachmentsQueries + "`);\n",
    expected: ["I1"],
  },
  {
    condition: "DynamicImportWithOptionsOfAnotherModulesInternals",
    source: systemInfoComponent,
    content: `const queries = await import("${attachmentsQueries}", { with: { type: "json" } });\n`,
    expected: ["I1"],
  },
  {
    condition: "MultiLineDynamicImportWithATrailingComma",
    source: systemInfoComponent,
    content: `const queries = await import(\n  "${attachmentsQueries}",\n);\n`,
    expected: ["I1"],
  },
  {
    condition: "DynamicImportWithASubstitution",
    source: systemInfoComponent,
    content: "const queries = await import(`@/features/platform/${module}/files/server/queries`);\n",
    expected: [],
  },
  {
    condition: "ImportInsideALineComment",
    source: systemInfoComponent,
    content: `// import { listAttachments } from "${attachmentsQueries}";\nexport const x = 1;\n`,
    expected: [],
  },
  {
    condition: "ImportInsideABlockComment",
    source: systemInfoComponent,
    content: `/*\nimport { listAttachments } from "${attachmentsQueries}";\n*/\nexport const x = 1;\n`,
    expected: [],
  },
  {
    condition: "ImportAfterADirectiveOnTheSameLine",
    source: systemInfoComponent,
    content: `"use client"; import { listAttachments } from "${attachmentsQueries}";\n`,
    expected: ["I1"],
  },
  {
    condition: "ImportAfterABlockCommentMarkerInsideAString",
    source: systemInfoComponent,
    content: `const glob = "src/**/*.ts";\nimport { listAttachments } from "${attachmentsQueries}";\n`,
    expected: ["I1"],
  },
  {
    condition: "ImportAfterABlockCommentMarkerInsideATemplateLiteral",
    source: systemInfoComponent,
    content: "const glob = `src/**/${name}`;\n" + `import { listAttachments } from "${attachmentsQueries}";\n`,
    expected: ["I1"],
  },
  {
    condition: "ImportAfterABlockCommentMarkerInsideARegularExpression",
    source: systemInfoComponent,
    content: `const pattern = /a\\/*/;\nimport { listAttachments } from "${attachmentsQueries}";\n`,
    expected: ["I1"],
  },
  {
    condition: "DynamicImportAfterAMediaTypeWildcardInJsxText",
    source: systemInfoComponent,
    content: [
      "export function UploadHint() {",
      "  return <p>Accepted: image/*, */* and src/*.ts up to 25 MB.</p>;",
      "}",
      `export const loadQueries = () => import("${attachmentsQueries}");`,
      "",
    ].join("\n"),
    expected: ["I1"],
  },
  {
    condition: "ImportAfterAStringEndingWithFrom",
    source: systemInfoComponent,
    content: `export const columns = [\n  { header: "Valid from" },\n];\nimport { listAttachments } from "${attachmentsQueries}";\n`,
    expected: ["I1"],
  },
  {
    condition: "ReExportAfterAStringEndingWithFrom",
    source: systemInfoComponent,
    content: `export const hint = "Copy from";\nexport * from "${attachmentsQueries}";\n`,
    expected: ["I1"],
  },
  {
    condition: "IndexReExportingItsOwnQueries",
    source: "features/platform/attachments/index.ts",
    content: 'export { listAttachments } from "./files/server/queries";\n',
    expected: ["I7"],
  },
  {
    condition: "IndexReExportingItsOwnActionsThroughTheAlias",
    source: "features/platform/attachments/index.ts",
    content: 'export { deleteAttachment } from "@/features/platform/attachments/files/server/actions";\n',
    expected: ["I7"],
  },
  {
    condition: "IndexReExportingItsOwnSchema",
    source: "features/platform/attachments/index.ts",
    content: 'export { uploadSchema } from "./files/forms/upload.schema";\n',
    expected: ["I7"],
  },
  {
    condition: "ComponentImportingItsOwnModulesQueries",
    source: attachmentsComponent,
    content: 'import { listAttachments } from "../server/queries";\n',
    expected: [],
  },
  {
    condition: "TypeofImportOfAnotherModulesInternals",
    source: systemInfoComponent,
    content: `type Queries = typeof import("${attachmentsQueries}");\n`,
    expected: ["I1"],
  },
  {
    condition: "AnotherModulesPublicSurface",
    source: systemInfoComponent,
    content: [
      'import { attachmentsNavigation } from "@/features/platform/attachments";',
      'import { AttachmentsOverview } from "@/features/platform/attachments/index";',
      'import type { AttachmentsOverviewProps } from "../../../attachments/index.ts";',
    ].join("\n"),
    expected: [],
  },
  {
    condition: "SiblingFeatureOfTheSameModuleByRelativePath",
    source: systemInfoComponent,
    content: 'import { RecentStartupsTable } from "../../startups/components/recent-startups-table";\n',
    expected: [],
  },
  {
    condition: "RelativePathIntoAnotherModule",
    source: systemInfoComponent,
    content: 'import { AttachmentsTable } from "../../../attachments/files/components/attachments-table";\n',
    expected: ["I1"],
  },
  {
    condition: "DetourPathIntoAnotherModule",
    source: systemInfoComponent,
    content: 'import { listAttachments } from "../detour/../../../attachments/files/server/queries";\n',
    expected: ["I1"],
  },
  {
    condition: "UpperCaseAliasIntoAnotherModule",
    source: systemInfoComponent,
    content: 'import { listAttachments } from "@/Features/Platform/Attachments/files/server/queries";\n',
    expected: ["I1"],
  },
  {
    condition: "RegistryIntoAModulesNavigationFile",
    source: "features/registry.ts",
    content: 'import { attachmentsNavigation } from "@/features/platform/attachments/nav";\n',
    expected: ["I1"],
  },
  {
    condition: "DomainSharedFromTheSameDomain",
    source: invoiceComponent,
    content: [
      'import { MoneyCell } from "../../../_shared/money-cell";',
      'import { StatusBadge } from "@/features/finance/_shared/status-badge";',
    ].join("\n"),
    expected: [],
  },
  {
    condition: "DomainSharedFromAnotherDomain",
    source: attachmentsComponent,
    content: 'import { MoneyCell } from "@/features/finance/_shared/money-cell";\n',
    expected: ["I2"],
  },
  {
    condition: "RouteImportingModuleSurfacesTheRegistrySharedAndPackages",
    source: "app/(app)/layout.tsx",
    content: [
      'import { Card } from "@dewiride/erp-ui/components/ui/card";',
      'import type { ReactNode } from "react";',
      'import { navigation } from "@/features/registry";',
      'import { attachmentsNavigation } from "@/features/platform/attachments";',
      'import { SystemInfoOverview } from "@/features/platform/system-info/index";',
      'import { AppShell } from "@/shared/layout/app-shell";',
    ].join("\n"),
    expected: [],
  },
  {
    condition: "RouteImportingModuleInternals",
    source: "app/(app)/platform/attachments/layout.tsx",
    content: 'import { attachmentsNavigation } from "@/features/platform/attachments/nav";\n',
    expected: ["I3"],
  },
  {
    condition: "RouteImportingADomainShared",
    source: "app/(app)/finance/sales/page.tsx",
    content: 'import { MoneyCell } from "@/features/finance/_shared/money-cell";\n',
    expected: ["I3"],
  },
  {
    condition: "RouteImportingByRelativePath",
    source: "app/(app)/platform/attachments/loading.tsx",
    content: ['import { Skeleton } from "./skeleton";', 'import { AppShell } from "../../../../shared/layout/app-shell";'].join("\n"),
    expected: ["I3", "I3"],
  },
  {
    condition: "RouteImportingAnotherRouteFile",
    source: "app/(app)/platform/attachments/page.tsx",
    content: 'import AppLayout from "@/app/(app)/layout";\n',
    expected: ["I3"],
  },
  {
    condition: "ModuleImportingTheRegistry",
    source: attachmentsComponent,
    content: 'import { navigation } from "@/features/registry";\n',
    expected: ["I4"],
  },
  {
    condition: "SharedImportingShared",
    source: "shared/layout/app-shell.tsx",
    content: ['import { Wordmark } from "@/shared/brand/wordmark";', 'import type { NavigationEntry } from "./navigation-entry";'].join(
      "\n",
    ),
    expected: [],
  },
  {
    condition: "ProxyImportingShared",
    source: "proxy.ts",
    content: ['import { apiBasePath } from "./shared/api/base-path";', 'import { readServerEnv } from "@/shared/config/env.schema";'].join(
      "\n",
    ),
    expected: [],
  },
  {
    condition: "InstrumentationImportingItsNodeRuntimeFile",
    source: "instrumentation.ts",
    content: 'const { registerNodeRuntime } = await import("./instrumentation.node");\n',
    expected: [],
  },
  {
    condition: "SharedImportingTheRegistry",
    source: "shared/layout/app-shell.tsx",
    content: 'import { navigation } from "@/features/registry";\n',
    expected: ["I5"],
  },
  {
    condition: "SharedImportingAModuleSurfaceByRelativePath",
    source: "shared/layout/app-shell.tsx",
    content: 'import { attachmentsNavigation } from "../../features/platform/attachments";\n',
    expected: ["I5"],
  },
  {
    condition: "ProxyImportingAModule",
    source: "proxy.ts",
    content: 'import { LoginCard } from "./features/identity/auth";\n',
    expected: ["I5"],
  },
  {
    condition: "InstrumentationImportingAModule",
    source: "instrumentation.node.ts",
    content: 'import { systemInfoNavigation } from "@/features/platform/system-info";\n',
    expected: ["I5"],
  },
  {
    condition: "ModuleImportingShared",
    source: attachmentsComponent,
    content: 'import { callApi } from "@/shared/api/client";\n',
    expected: [],
  },
  {
    condition: "ModuleImportingARouteFile",
    source: attachmentsComponent,
    content: 'import AppLayout from "@/app/(app)/layout";\n',
    expected: ["I6"],
  },
  {
    condition: "SharedImportingARouteFileByRelativePath",
    source: "shared/layout/app-shell.tsx",
    content: 'import RootLayout from "../../app/layout";\n',
    expected: ["I6"],
  },
  {
    condition: "PackagesAndPathsOutsideTheSourceRoot",
    source: "proxy.ts",
    content: [
      'import { NextResponse } from "next/server";',
      'import nextConfig from "../next.config";',
      'import "./shared/api/base-path";',
    ].join("\n"),
    expected: [],
  },
  {
    condition: "FileThatIsNotCode",
    source: "app/icon.svg",
    content: `<svg><!-- import "${attachmentsQueries}" --></svg>`,
    expected: [],
  },
];

for (const { condition, source, content, expected } of cases) {
  const rules = [...new Set(expected)];
  const outcome = rules.length === 0 ? "IsAllowed" : `Reports${rules.join("And")}`;
  test(`importViolations_${condition}_${outcome}`, () => {
    assert.deepEqual(found(source, content), [...expected].sort());
  });
}

test("importViolations_Violation_NamesTheFileTheRuleAndTheSpecifier", () => {
  const [violation] = importViolations([{ path: attachmentsComponent, content: 'import { navigation } from "@/features/registry";\n' }]);
  assert.deepEqual(violation, {
    path: attachmentsComponent,
    rule: "I4",
    message: 'import "@/features/registry" — features/registry.ts is imported only from app/',
  });
});

test("importViolations_SharedSource_NamesSharedAsTheImporter", () => {
  const [violation] = importViolations([
    { path: "shared/layout/app-shell.tsx", content: `import { listAttachments } from "${attachmentsQueries}";\n` },
  ]);
  assert.equal(violation?.rule, "I5");
  assert.match(violation?.message ?? "", /shared\/ never imports features\//);
  assert.doesNotMatch(violation?.message ?? "", /features\/shared/);
});

test("importViolations_SameSpecifierImportedTwice_IsReportedOnce", () => {
  const content = `import type { AttachmentPage } from "${attachmentsQueries}";\nimport { listAttachments } from "${attachmentsQueries}";\n`;
  assert.deepEqual(found(systemInfoComponent, content), ["I1"]);
});
