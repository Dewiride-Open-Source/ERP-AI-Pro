import assert from "node:assert/strict";
import { test } from "node:test";

import type { WebSourceFile } from "../lib/web-source.ts";
import { structureViolations } from "../lib/web-structure.ts";

const actions = '"use server";\n\nimport { z } from "zod";\n';
const queries = 'import "server-only";\n\nimport { cache } from "react";\n';

const file = (path: string, content = ""): WebSourceFile => ({ path, content });

const compliantTree: readonly WebSourceFile[] = [
  file("app/layout.tsx"),
  file("app/page.tsx"),
  file("app/error.tsx"),
  file("app/global-error.tsx"),
  file("app/not-found.tsx"),
  file("app/icon.svg"),
  file("app/healthz/route.ts"),
  file("app/(auth)/layout.tsx"),
  file("app/(auth)/login/page.tsx"),
  file("app/(app)/layout.tsx"),
  file("app/(app)/not-found.tsx"),
  file("app/(app)/platform/attachments/layout.tsx"),
  file("app/(app)/platform/attachments/loading.tsx"),
  file("app/(app)/platform/attachments/page.tsx"),
  file("features/registry.ts"),
  file("features/platform/attachments/index.ts"),
  file("features/platform/attachments/nav.ts"),
  file("features/platform/attachments/files/components/attachments-overview.tsx"),
  file("features/platform/attachments/files/server/actions.ts", actions),
  file("features/platform/attachments/files/server/queries.ts", queries),
  file("shared/layout/app-shell.tsx"),
  file("proxy.ts"),
  file("instrumentation.ts"),
];

const tree = (...changes: WebSourceFile[]): WebSourceFile[] => {
  const byPath = new Map(compliantTree.map((entry) => [entry.path, entry]));
  for (const change of changes) byPath.set(change.path, change);
  return [...byPath.values()];
};

const found = (files: readonly WebSourceFile[]): string[] =>
  structureViolations(files)
    .map((violation) => `${violation.rule} ${violation.path}`)
    .sort();

type Case = { readonly condition: string; readonly files: readonly WebSourceFile[]; readonly expected: readonly string[] };

const featureRoot = "features/platform/attachments/files";

const cases: readonly Case[] = [
  {
    condition: "EverySegmentAndMetadataFileInsideAGroup",
    files: [
      file("app/(app)/reports/layout.tsx"),
      file("app/(app)/reports/page.tsx"),
      file("app/(app)/reports/loading.tsx"),
      file("app/(app)/reports/error.tsx"),
      file("app/(app)/reports/not-found.tsx"),
      file("app/(app)/reports/template.tsx"),
      file("app/(app)/reports/icon.tsx"),
      file("app/(app)/reports/icon1.png"),
      file("app/(app)/reports/apple-icon.png"),
      file("app/(app)/reports/opengraph-image.png"),
      file("app/(app)/reports/opengraph-image.alt.txt"),
      file("app/(app)/reports/twitter-image.tsx"),
      file("app/(app)/reports/sitemap.ts"),
      file("app/(app)/reports/export/route.ts"),
    ],
    expected: [],
  },
  {
    condition: "RootOnlyMetadataFilesAtTheRoot",
    files: [file("app/favicon.ico"), file("app/robots.ts"), file("app/manifest.webmanifest"), file("app/sitemap.xml")],
    expected: [],
  },
  {
    condition: "ComponentFileInASegment",
    files: [file("app/(app)/platform/attachments/attachments-skeleton.tsx")],
    expected: ["S1 app/(app)/platform/attachments/attachments-skeleton.tsx"],
  },
  {
    condition: "SpecialFileWithTheWrongExtension",
    files: [file("app/(app)/platform/attachments/page.ts"), file("app/(app)/platform/attachments/route.tsx")],
    expected: ["S1 app/(app)/platform/attachments/page.ts", "S1 app/(app)/platform/attachments/route.tsx"],
  },
  {
    condition: "RootOnlyFileInsideAGroup",
    files: [file("app/(app)/global-error.tsx"), file("app/(app)/favicon.ico"), file("app/(app)/robots.ts")],
    expected: ["S1 app/(app)/favicon.ico", "S1 app/(app)/global-error.tsx", "S1 app/(app)/robots.ts"],
  },
  {
    condition: "ExperimentalFileConvention",
    files: [file("app/(app)/forbidden.tsx"), file("app/(app)/unauthorized.tsx"), file("app/global-not-found.tsx")],
    expected: ["S1 app/(app)/forbidden.tsx", "S1 app/(app)/unauthorized.tsx", "S1 app/global-not-found.tsx"],
  },
  {
    condition: "DynamicCatchAllOptionalCatchAllAndSlotSegments",
    files: [
      file("app/(app)/finance/sales/invoices/[invoiceId]/page.tsx"),
      file("app/(app)/help/[...topic]/page.tsx"),
      file("app/(app)/guides/[[...section]]/page.tsx"),
      file("app/(app)/finance/@summary/default.tsx"),
    ],
    expected: [],
  },
  {
    condition: "PrivateFolder",
    files: [file("app/(app)/platform/_parts/page.tsx")],
    expected: ["S2 app/(app)/platform/_parts/"],
  },
  {
    condition: "InterceptingRouteFolder",
    files: [file("app/(app)/platform/(.)preview/page.tsx")],
    expected: ["S2 app/(app)/platform/(.)preview/"],
  },
  {
    condition: "RouteHandlersInARootFolder",
    files: [file("app/healthz/live/route.ts"), file("app/healthz/ready/route.ts")],
    expected: [],
  },
  {
    condition: "PageOutsideTheGroups",
    files: [file("app/dashboard/page.tsx")],
    expected: ["S3 app/dashboard/page.tsx"],
  },
  {
    condition: "LayoutBesideARootRouteHandler",
    files: [file("app/healthz/layout.tsx")],
    expected: ["S3 app/healthz/layout.tsx"],
  },
  {
    condition: "SegmentFilesThatDoNotBelongAtTheRoot",
    files: [file("app/loading.tsx"), file("app/template.tsx"), file("app/route.ts")],
    expected: ["S3 app/loading.tsx", "S3 app/route.ts", "S3 app/template.tsx"],
  },
  {
    condition: "ThirdRouteGroup",
    files: [file("app/(marketing)/pricing/page.tsx")],
    expected: ["S3 app/(marketing)/"],
  },
  {
    condition: "RouteGroupNestedInsideAGroup",
    files: [file("app/(app)/(finance)/finance/sales/page.tsx"), file("app/(app)/(finance)/layout.tsx")],
    expected: [],
  },
  {
    condition: "EveryFeatureSubfolderADomainSharedAndAModuleWithoutNav",
    files: [
      file(`${featureRoot}/components/table/attachments-table.tsx`),
      file(`${featureRoot}/forms/upload.schema.ts`),
      file(`${featureRoot}/lists/attachments.list.ts`),
      file(`${featureRoot}/hooks/use-upload.ts`),
      file(`${featureRoot}/ai/tagging/tag-suggestions.tsx`),
      file("features/platform/_shared/status-badge.tsx"),
      file("features/identity/auth/index.ts"),
      file("features/identity/auth/login/components/login-card.tsx"),
    ],
    expected: [],
  },
  {
    condition: "LooseFileInFeatures",
    files: [file("features/navigation.ts")],
    expected: ["S4 features/navigation.ts"],
  },
  {
    condition: "FileInADomainFolder",
    files: [file("features/platform/helpers.ts")],
    expected: ["S4 features/platform/helpers.ts"],
  },
  {
    condition: "ModuleRootFileOtherThanIndexAndNav",
    files: [file("features/platform/attachments/types.ts")],
    expected: ["S4 features/platform/attachments/types.ts"],
  },
  {
    condition: "ModuleWithoutIndex",
    files: [file("features/platform/audit/log/components/audit-table.tsx"), file("features/platform/audit/nav.ts")],
    expected: ["S4 features/platform/audit/"],
  },
  {
    condition: "LooseFileInAFeature",
    files: [file(`${featureRoot}/utils.ts`)],
    expected: [`S4 ${featureRoot}/utils.ts`],
  },
  {
    condition: "UnknownFeatureSubfolder",
    files: [file(`${featureRoot}/lib/format.ts`), file(`${featureRoot}/lib/parse.ts`)],
    expected: [`S4 ${featureRoot}/lib/`],
  },
  {
    condition: "ServerFilesWithCommentsAndSingleQuotes",
    files: [
      file(
        `${featureRoot}/server/actions.ts`,
        "/* The API re-checks every rule. */\n// Runs on the server only.\n'use server'\n\nimport { z } from 'zod'\n",
      ),
      file(`${featureRoot}/server/queries.ts`, "import { cache } from 'react'\nimport 'server-only'\n"),
    ],
    expected: [],
  },
  {
    condition: "ActionsSavedWithAByteOrderMark",
    files: [file(`${featureRoot}/server/actions.ts`, `${String.fromCodePoint(0xfeff)}${actions}`)],
    expected: [],
  },
  {
    condition: "FormsListsHooksAndAiFilesWithTheirTests",
    files: [
      file(`${featureRoot}/forms/upload.schema.ts`),
      file(`${featureRoot}/forms/upload.schema.test.ts`),
      file(`${featureRoot}/lists/attachments.list.ts`),
      file(`${featureRoot}/lists/attachments.list.test.ts`),
      file(`${featureRoot}/hooks/use-upload-progress.tsx`),
      file(`${featureRoot}/hooks/use-upload-progress.test.ts`),
      file(`${featureRoot}/ai/tagging/prompts/tag-suggestions.prompt.md`),
    ],
    expected: [],
  },
  {
    condition: "OtherFileInServer",
    files: [file(`${featureRoot}/server/mappers.ts`)],
    expected: [`S5 ${featureRoot}/server/mappers.ts`],
  },
  {
    condition: "NestedFolderInServer",
    files: [file(`${featureRoot}/server/internal/queries.ts`, queries)],
    expected: [`S5 ${featureRoot}/server/internal/queries.ts`],
  },
  {
    condition: "ActionsWithoutUseServer",
    files: [file(`${featureRoot}/server/actions.ts`, 'import { z } from "zod";\n')],
    expected: [`S5 ${featureRoot}/server/actions.ts`],
  },
  {
    condition: "UseServerAfterAnImport",
    files: [file(`${featureRoot}/server/actions.ts`, 'import { z } from "zod";\n"use server";\n')],
    expected: [`S5 ${featureRoot}/server/actions.ts`],
  },
  {
    condition: "QueriesWithoutServerOnly",
    files: [file(`${featureRoot}/server/queries.ts`, 'import { cache } from "react";\n')],
    expected: [`S5 ${featureRoot}/server/queries.ts`],
  },
  {
    condition: "ServerOnlyImportWithATrailingComment",
    files: [file(`${featureRoot}/server/queries.ts`, 'import "server-only"; // The API client carries the cookie.\n')],
    expected: [],
  },
  {
    condition: "ServerOnlyImportInsideABlockComment",
    files: [file(`${featureRoot}/server/queries.ts`, '/*\nimport "server-only";\n*/\nimport { cache } from "react";\n')],
    expected: [`S5 ${featureRoot}/server/queries.ts`],
  },
  {
    condition: "UseServerInsideALeadingBlockComment",
    files: [file(`${featureRoot}/server/actions.ts`, '/* "use server"; */\nimport { z } from "zod";\n')],
    expected: [`S5 ${featureRoot}/server/actions.ts`],
  },
  {
    condition: "FormsFileThatIsNotASchema",
    files: [file(`${featureRoot}/forms/upload-form.ts`)],
    expected: [`S5 ${featureRoot}/forms/upload-form.ts`],
  },
  {
    condition: "SchemaTestWithoutItsSchema",
    files: [file(`${featureRoot}/forms/upload.schema.test.ts`)],
    expected: [`S5 ${featureRoot}/forms/upload.schema.test.ts`],
  },
  {
    condition: "ListsFileThatIsNotAListDefinition",
    files: [file(`${featureRoot}/lists/attachments-columns.ts`)],
    expected: [`S5 ${featureRoot}/lists/attachments-columns.ts`],
  },
  {
    condition: "ListTestWithoutItsListDefinition",
    files: [file(`${featureRoot}/lists/attachments.list.test.ts`)],
    expected: [`S5 ${featureRoot}/lists/attachments.list.test.ts`],
  },
  {
    condition: "HookWithoutTheUsePrefix",
    files: [file(`${featureRoot}/hooks/upload-progress.ts`)],
    expected: [`S5 ${featureRoot}/hooks/upload-progress.ts`],
  },
  {
    condition: "HookTestWithoutItsHook",
    files: [file(`${featureRoot}/hooks/use-upload.test.ts`)],
    expected: [`S5 ${featureRoot}/hooks/use-upload.test.ts`],
  },
  {
    condition: "LooseFileInAi",
    files: [file(`${featureRoot}/ai/prompt.ts`)],
    expected: [`S5 ${featureRoot}/ai/prompt.ts`],
  },
  {
    condition: "KebabCaseNamesWithDottedSuffixes",
    files: [
      file(`${featureRoot}/components/content-types.ts`),
      file(`${featureRoot}/components/content-types.test.ts`),
      file("app/(app)/platform/system-info/page.tsx"),
      file("shared/config/env.schema.ts"),
    ],
    expected: [],
  },
  {
    condition: "PascalCaseComponentFile",
    files: [file(`${featureRoot}/components/UploadPanel.tsx`)],
    expected: [`S6 ${featureRoot}/components/UploadPanel.tsx`],
  },
  {
    condition: "CamelCaseFeatureFolder",
    files: [file("features/platform/attachments/fileUploads/components/upload-panel.tsx")],
    expected: ["S6 features/platform/attachments/fileUploads/"],
  },
  {
    condition: "SharedFolderBelowTheDomain",
    files: [file(`${featureRoot}/components/_shared/row.tsx`)],
    expected: [`S6 ${featureRoot}/components/_shared/`],
  },
  {
    condition: "UpperCaseRouteSegment",
    files: [file("app/(app)/Platform/page.tsx")],
    expected: ["S6 app/(app)/Platform/"],
  },
  {
    condition: "SnakeCaseDynamicSegment",
    files: [file("app/(app)/invoices/[invoice_id]/page.tsx")],
    expected: ["S6 app/(app)/invoices/[invoice_id]/"],
  },
  {
    condition: "FileAtTheRootOfShared",
    files: [file("shared/utils.ts")],
    expected: ["S6 shared/utils.ts"],
  },
];

test("structureViolations_CompliantTree_ReportsNothing", () => {
  assert.deepEqual(found(compliantTree), []);
});

for (const { condition, files, expected } of cases) {
  const rules = [...new Set(expected.map((entry) => entry.split(" ")[0]))];
  const outcome = rules.length === 0 ? "IsAccepted" : `Reports${rules.join("And")}`;
  test(`structureViolations_${condition}_${outcome}`, () => {
    assert.deepEqual(found(tree(...files)), [...expected].sort());
  });
}

test("structureViolations_EveryViolation_NamesItsRuleAndReason", () => {
  const [violation] = structureViolations(tree(file(`${featureRoot}/server/queries.ts`, 'import { cache } from "react";\n')));
  assert.deepEqual(violation, {
    path: `${featureRoot}/server/queries.ts`,
    rule: "S5",
    message: 'server/queries.ts imports "server-only"',
  });
});

test("structureViolations_FolderViolationUnderManyFiles_IsReportedOnce", () => {
  const files = tree(file("app/(app)/platform/_parts/page.tsx"), file("app/(app)/platform/_parts/layout.tsx"));
  assert.deepEqual(found(files), ["S2 app/(app)/platform/_parts/"]);
});
