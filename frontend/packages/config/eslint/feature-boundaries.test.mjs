import assert from "node:assert/strict";
import { join } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";

import { Linter } from "eslint";

import { nextConfig } from "./next.mjs";
import { reactLibraryConfig } from "./react-library.mjs";

const restrictedRules = new Set([
  "no-restricted-imports",
  "no-restricted-syntax",
  "no-restricted-globals",
  "no-restricted-properties",
]);

const presets = {
  web: { config: nextConfig, root: fileURLToPath(new URL("../../../apps/web/", import.meta.url)) },
  ui: { config: reactLibraryConfig, root: fileURLToPath(new URL("../../ui/", import.meta.url)) },
};

const moduleInternals = ["no-restricted-imports", /only through its public surface/];
const domainSharedAlias = ["no-restricted-imports", /_shared code with a relative path/];
const appAlias = ["no-restricted-imports", /Nothing imports from app\//];
const detour = ["no-restricted-imports", /without a '\.\.' detour/];
const currentFolderDetour = ["no-restricted-imports", /without a '\.\/' after its start/];
const moduleEscape = ["no-restricted-imports", /stays inside its module/];
const featuresFromShared = ["no-restricted-imports", /shared\/ never imports features\//];
const relativeFromApp = ["no-restricted-imports", /never a relative path/];
const routeImports = ["no-restricted-imports", /Route files import only /];
const routeDynamicImports = ["no-restricted-syntax", /Route files import only /];
const documentImports = ["no-restricted-imports", /The root layout and the global error import only /];
const documentDynamicImports = ["no-restricted-syntax", /The root layout and the global error import only /];
const apiClientValues = [
  "no-restricted-imports",
  /import @dewiride\/erp-api-client types with `import type`/,
];
const apiClientDynamicImport = [
  "no-restricted-syntax",
  /import @dewiride\/erp-api-client types with `import type`/,
];
const httpGlobal = ["no-restricted-globals", /Only shared\/api sends HTTP requests/];
const httpProperty = ["no-restricted-properties", /Only shared\/api sends HTTP requests/];
const routeMarkup = ["no-restricted-syntax", /A route file only composes/];
const documentMarkup = ["no-restricted-syntax", /render only <html> and <body>/];
const objectHref = ["no-restricted-syntax", /an object href is not checked/];
const typeRole = ["no-restricted-syntax", /Put a type-role class/];
const webAppFromPackage = ["no-restricted-imports", /never imports from apps\/web/];

const componentFile = "src/features/platform/attachments/files/components/upload-panel.tsx";
const startupsConsumer = "src/features/platform/system-info/info/components/system-info-overview.tsx";
const primitivesFile = "src/features/platform/design/kitchen-sink/components/primitives/actions-showcase.tsx";
const deepestModuleFile = "src/features/platform/attachments/a/b/c/d/e/f/g/h/deep.ts";
const domainSharedFile = "src/features/platform/_shared/money.ts";
const pageFile = "src/app/(app)/platform/attachments/page.tsx";
const loadingFile = "src/app/(app)/platform/attachments/loading.tsx";

const cases = [
  {
    title: "nextConfig_ModuleImportsAnotherModuleThroughItsIndex_IsAllowed",
    file: componentFile,
    code: 'export { SystemInfoOverview } from "@/features/platform/system-info";\nexport { systemInfoNavigation } from "@/features/platform/system-info/index";',
    expected: [],
  },
  {
    title: "nextConfig_ModuleImportsAnotherModulesInternalsByAlias_IsReported",
    file: componentFile,
    code: 'import { getSystemInfo } from "@/features/platform/system-info/info/server/queries";\nexport { systemInfoNavigation } from "@/features/platform/system-info/nav";\nimport type { Startup } from "@/features/platform/system-info/startups/server/queries";\nimport "@/features/platform/system-info/index/extra";',
    expected: [moduleInternals, moduleInternals, moduleInternals, moduleInternals],
  },
  {
    title: "nextConfig_ModuleImportsItsDomainSharedRelatively_IsAllowed",
    file: startupsConsumer,
    code: 'import { formatRupees } from "../../../_shared/money";',
    expected: [],
  },
  {
    title: "nextConfig_ModuleImportsItsDomainSharedFolderIndex_IsAllowed",
    file: startupsConsumer,
    code: 'import { formatRupees } from "../../../_shared";',
    expected: [],
  },
  {
    title: "nextConfig_ModuleImportsASiblingNamedLikeDomainShared_IsReported",
    file: startupsConsumer,
    code: 'import { formatRupees } from "../../../_sharedx";',
    expected: [moduleEscape],
  },
  {
    title: "nextConfig_ModuleImportsItsDomainSharedByAlias_IsReported",
    file: startupsConsumer,
    code: 'import { formatRupees } from "@/features/platform/_shared/money";',
    expected: [domainSharedAlias],
  },
  {
    title: "nextConfig_SourceImportsAPathThatOnlyStartsWithApp_IsAllowed",
    file: componentFile,
    code: 'import { settings } from "@/application/settings";\nimport { AppShell } from "@/app-shell/frame";',
    expected: [],
  },
  {
    title: "nextConfig_SourceImportsFromApp_IsReported",
    file: componentFile,
    code: 'import RootLayout from "@/app/layout";',
    expected: [appAlias],
  },
  {
    title: "nextConfig_NormalisedRelativePath_IsAllowed",
    file: componentFile,
    code: 'import { attachmentsNavigation } from "../../nav";\nimport { deleteAttachment } from "../server/actions";\nimport { contentTypeLabel } from "./content-types";',
    expected: [],
  },
  {
    title: "nextConfig_RelativePathWithADetour_IsReported",
    file: componentFile,
    code: 'import { contentTypeLabel } from "./parts/../content-types";\nimport { formatBytes } from "@/shared/api/../format/sizes";',
    expected: [detour, detour],
  },
  {
    title: "nextConfig_RelativePathWithACurrentFolderSegmentAfterItsStart_IsReported",
    file: componentFile,
    code: 'import { SystemInfoOverview } from ".././../system-info";\nimport { contentTypeLabel } from "././content-types";',
    expected: [currentFolderDetour, currentFolderDetour],
  },
  {
    title: "nextConfig_SourceOnlyFileImportsAModulesSurfaceAndClientTypes_IsAllowed",
    file: "src/proxy.ts",
    code: 'import type { ErpApiClient } from "@dewiride/erp-api-client";\nimport { attachmentsNavigation } from "@/features/platform/attachments";\nimport { apiBasePath } from "./shared/api/base-path";',
    expected: [],
  },
  {
    title: "nextConfig_SourceOnlyFileImportsModuleInternalsAndClientValues_IsReported",
    file: "src/proxy.ts",
    code: 'import { attachmentsNavigation } from "@/features/platform/attachments/nav";\nimport { connect } from "@dewiride/erp-api-client";',
    expected: [moduleInternals, apiClientValues],
  },
  {
    title: "nextConfig_ModuleIndexReExportsItsOwnFeature_IsAllowed",
    file: "src/features/platform/attachments/index.ts",
    code: 'export { AttachmentsOverview } from "./files/components/attachments-overview";\nexport { attachmentsNavigation } from "./nav";',
    expected: [],
  },
  {
    title: "nextConfig_ModuleIndexReExportsAnotherModuleRelatively_IsReported",
    file: "src/features/platform/attachments/index.ts",
    code: 'export { SystemInfoOverview } from "../system-info/info/components/system-info-overview";',
    expected: [moduleEscape],
  },
  {
    title: "nextConfig_FeatureImportsASiblingFeatureOfItsModule_IsAllowed",
    file: startupsConsumer,
    code: 'import { RecentStartupsTable } from "../../startups/components/recent-startups-table";',
    expected: [],
  },
  {
    title: "nextConfig_FeatureImportsAnotherModuleRelatively_IsReported",
    file: startupsConsumer,
    code: 'import { AttachmentsOverview } from "../../../attachments";\nimport { LoginCard } from "../../../../identity/auth";',
    expected: [moduleEscape, moduleEscape],
  },
  {
    title: "nextConfig_ThreeFoldersDeepImportsItsOwnFeature_IsAllowed",
    file: primitivesFile,
    code: 'import { Specimen } from "../specimen";\nimport { kitchenSinkSections } from "./../sections";',
    expected: [],
  },
  {
    title: "nextConfig_ThreeFoldersDeepLeavesItsModule_IsReported",
    file: primitivesFile,
    code: 'import { SystemInfoOverview } from "./../../../../system-info";',
    expected: [moduleEscape],
  },
  {
    title: "nextConfig_DeepestCoveredFolderClimbsToItsModuleRoot_IsAllowed",
    file: deepestModuleFile,
    code: 'import { attachmentsNavigation } from "../../../../../../../../nav";',
    expected: [],
  },
  {
    title: "nextConfig_DeepestCoveredFolderClimbsOutOfItsModule_IsReported",
    file: deepestModuleFile,
    code: 'import { systemInfoNavigation } from "../../../../../../../../../system-info/nav";',
    expected: [moduleEscape],
  },
  {
    title: "nextConfig_DomainSharedImportsAModuleThroughItsIndex_IsAllowed",
    file: domainSharedFile,
    code: 'import { attachmentsNavigation } from "@/features/platform/attachments";\nimport { formatBytes } from "./sizes";',
    expected: [],
  },
  {
    title: "nextConfig_DomainSharedImportsAModuleRelatively_IsReported",
    file: domainSharedFile,
    code: 'import { getAttachments } from "../attachments/files/server/queries";',
    expected: [moduleEscape],
  },
  {
    title: "nextConfig_SharedImportsShared_IsAllowed",
    file: "src/shared/layout/app-shell.tsx",
    code: 'import { Wordmark } from "@/shared/brand/wordmark";\nimport { getFeatureFlags } from "../feature-flags/queries";\nimport { NotFoundMessage } from "./not-found-message";',
    expected: [],
  },
  {
    title: "nextConfig_SharedImportsFeatures_IsReported",
    file: "src/shared/layout/app-shell.tsx",
    code: 'import { navigation } from "@/features/registry";\nimport { AttachmentsOverview } from "@/features/platform/attachments";\nimport { LoginCard } from "../../features/identity/auth";',
    expected: [featuresFromShared, featuresFromShared, featuresFromShared],
  },
  {
    title: "nextConfig_SharedImportsAppOrADetour_IsReported",
    file: "src/shared/layout/app-shell.tsx",
    code: 'import RootLayout from "@/app/layout";\nimport { Wordmark } from "./parts/../wordmark";\nimport { NotFoundMessage } from "../layout/./not-found-message";',
    expected: [appAlias, detour, currentFolderDetour],
  },
  {
    title: "nextConfig_SharedApiImportsAppOrADetour_IsReported",
    file: "src/shared/api/client.ts",
    code: 'import RootLayout from "@/app/layout";\nimport { apiBasePath } from "./parts/../base-path";',
    expected: [appAlias, detour],
  },
  {
    title: "nextConfig_SharedApiImportsFeatures_IsReported",
    file: "src/shared/api/client.ts",
    code: 'import { navigation } from "@/features/registry";',
    expected: [featuresFromShared],
  },
  {
    title: "nextConfig_RouteImportsItsAllowList_IsAllowed",
    file: pageFile,
    code: [
      'import type { Metadata } from "next";',
      'import { notFound } from "next/navigation";',
      'import type { ReactNode } from "react";',
      'import { navigation } from "@/features/registry";',
      'import { AttachmentsOverview } from "@/features/platform/attachments";',
      'import { attachmentsNavigation } from "@/features/platform/attachments/index";',
      'import { requireFeature } from "@/shared/feature-flags/require-feature";',
    ].join("\n"),
    expected: [],
  },
  {
    title: "nextConfig_RouteImportsOutsideItsAllowList_IsReported",
    file: loadingFile,
    code: [
      'import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";',
      'import { attachmentsNavigation } from "@/features/platform/attachments/nav";',
      'import { formatRupees } from "@/features/platform/_shared/money";',
      'import { AttachmentsOverview } from "@/Features/Platform/Attachments";',
      'import { connect } from "@dewiride/erp-api-client";',
      'import type { AttachmentResponse } from "@dewiride/erp-api-client";',
      'import { paging } from "@/shared/../features/registry";',
      'import { GeistSans } from "geist/font/sans";',
    ].join("\n"),
    expected: [
      routeImports,
      routeImports,
      routeImports,
      routeImports,
      routeImports,
      routeImports,
      routeImports,
      routeImports,
    ],
  },
  {
    title: "nextConfig_RouteImportsRelatively_IsReported",
    file: loadingFile,
    code: 'import { Skeleton } from "./skeleton";\nimport { AttachmentsOverview } from "../../../../features/platform/attachments";',
    expected: [relativeFromApp, relativeFromApp],
  },
  {
    title: "nextConfig_RootLayoutImportsTheDesignSystemAndFonts_IsAllowed",
    file: "src/app/layout.tsx",
    code: [
      'import "@dewiride/erp-ui/globals.css";',
      'import { ThemeProvider } from "@dewiride/erp-ui/components/theme/theme-provider";',
      'import { cn } from "@dewiride/erp-ui/lib/utils";',
      'import { GeistSans } from "geist/font/sans";',
      'import { headers } from "next/headers";',
      'import { publicEnv } from "@/shared/config/public-env";',
    ].join("\n"),
    expected: [],
  },
  {
    title: "nextConfig_RootLayoutImportsOutsideItsAllowList_IsReported",
    file: "src/app/layout.tsx",
    code: 'import { ShieldIcon } from "lucide-react";\nimport { Skeleton } from "./skeleton";',
    expected: [documentImports, relativeFromApp],
  },
  {
    title: "nextConfig_GlobalErrorImportsTheGlobalStylesheetAndFonts_IsAllowed",
    file: "src/app/global-error.tsx",
    code: [
      '"use client";',
      'import "@dewiride/erp-ui/globals.css";',
      'import { cn } from "@dewiride/erp-ui/lib/utils";',
      'import { GeistSans } from "geist/font/sans";',
      'import { ApplicationFailure } from "@/shared/layout/application-failure";',
    ].join("\n"),
    expected: [],
  },
  {
    title: "nextConfig_GlobalErrorImportsOutsideItsAllowList_IsReported",
    file: "src/app/global-error.tsx",
    code: 'import { ShieldIcon } from "lucide-react";\nimport { attachmentsNavigation } from "@/features/platform/attachments/nav";',
    expected: [documentImports, documentImports],
  },
  {
    title: "nextConfig_RouteImportsItsAllowListDynamically_IsAllowed",
    file: pageFile,
    code: 'export const overview = () => import("@/features/platform/attachments");\nexport const navigation = () => import("next/navigation");',
    expected: [],
  },
  {
    title: "nextConfig_RouteImportsOutsideItsAllowListDynamically_IsReported",
    file: loadingFile,
    code: [
      'export const nav = () => import("@/features/platform/attachments/nav");',
      'export const icons = () => import("lucide-react");',
      "export const shared = () => import(`@/shared/format/money`);",
      'export const skeleton = () => import("./skeleton");',
      'export const client = () => import("@dewiride/erp-api-client");',
    ].join("\n"),
    expected: [
      routeDynamicImports,
      routeDynamicImports,
      routeDynamicImports,
      routeDynamicImports,
      routeDynamicImports,
    ],
  },
  {
    title: "nextConfig_GlobalErrorImportsOutsideItsAllowListDynamically_IsReported",
    file: "src/app/global-error.tsx",
    code: 'export const icons = () => import("lucide-react");\nexport const styles = () => import("@dewiride/erp-ui/globals.css");',
    expected: [documentDynamicImports],
  },
  {
    title: "nextConfig_TypeImportFromTheApiClientOutsideSharedApi_IsAllowed",
    file: componentFile,
    code: 'import type { AttachmentResponse } from "@dewiride/erp-api-client";\nimport { type ProblemDetails } from "@dewiride/erp-api-client";\nexport type { StartupResponse } from "@dewiride/erp-api-client";',
    expected: [],
  },
  {
    title: "nextConfig_ValueImportFromTheApiClientOutsideSharedApi_IsReported",
    file: "src/shared/format/money.ts",
    code: 'import { connect, type ErpApiClient } from "@dewiride/erp-api-client";\nimport "@dewiride/erp-api-client";',
    expected: [apiClientValues, apiClientValues],
  },
  {
    title: "nextConfig_ValueImportFromTheApiClientInAModule_IsReported",
    file: componentFile,
    code: 'import { connect } from "@dewiride/erp-api-client";',
    expected: [apiClientValues],
  },
  {
    title: "nextConfig_ValueImportFromTheApiClientInADomainShared_IsReported",
    file: domainSharedFile,
    code: 'import { connect } from "@dewiride/erp-api-client";',
    expected: [apiClientValues],
  },
  {
    title: "nextConfig_DynamicImportOfTheApiClientOutsideSharedApi_IsReported",
    file: componentFile,
    code: 'export const load = () => import("@dewiride/erp-api-client");\nexport const loadTemplate = () => import(`@dewiride/erp-api-client`);',
    expected: [apiClientDynamicImport, apiClientDynamicImport],
  },
  {
    title: "nextConfig_DynamicImportOfTheApiClientInShared_IsReported",
    file: "src/shared/format/money.ts",
    code: 'export const load = () => import("@dewiride/erp-api-client");',
    expected: [apiClientDynamicImport],
  },
  {
    title: "nextConfig_TypeOfADynamicImportOfTheApiClient_IsAllowed",
    file: componentFile,
    code: 'export type Client = typeof import("@dewiride/erp-api-client");',
    expected: [],
  },
  {
    title: "nextConfig_DynamicImportOfTheApiClientInSharedApi_IsAllowed",
    file: "src/shared/api/client.ts",
    code: 'export const load = () => import("@dewiride/erp-api-client");',
    expected: [],
  },
  {
    title: "nextConfig_SharedApiUsesTheApiClientAndHttpGlobals_IsAllowed",
    file: "src/shared/api/client.ts",
    code: [
      'import { connect } from "@dewiride/erp-api-client";',
      "export const client = connect({ fetch });",
      "export const request = new XMLHttpRequest();",
      "export const events = new EventSource(globalThis.location.href);",
      "export const bound = globalThis.fetch;",
    ].join("\n"),
    expected: [],
  },
  {
    title: "nextConfig_HttpGlobalShadowedByAParameter_IsAllowed",
    file: componentFile,
    code: "export function run(fetch: () => void, client: { fetch: () => void }) {\n  fetch();\n  client.fetch();\n}",
    expected: [],
  },
  {
    title: "nextConfig_HttpGlobalOutsideSharedApi_IsReported",
    file: componentFile,
    code: [
      'export const page = await fetch("/api/platform/attachments");',
      "export const request = new XMLHttpRequest();",
      'export const events = new EventSource("/api/events");',
    ].join("\n"),
    expected: [httpGlobal, httpGlobal, httpGlobal],
  },
  {
    title: "nextConfig_HttpGlobalReachedThroughTheGlobalObject_IsReported",
    file: "src/shared/layout/app-shell.tsx",
    code: [
      'export const page = window.fetch("/api/platform/features");',
      "export const request = new globalThis.XMLHttpRequest();",
      'export const events = new self.EventSource("/api/events");',
    ].join("\n"),
    expected: [httpProperty, httpProperty, httpProperty],
  },
  {
    title: "nextConfig_RouteRendersOnlyComponents_IsAllowed",
    file: pageFile,
    code: 'import { AttachmentsOverview } from "@/features/platform/attachments";\nexport default function Page() {\n  return (\n    <>\n      <AttachmentsOverview />\n    </>\n  );\n}',
    expected: [],
  },
  {
    title: "nextConfig_RouteRendersAnHtmlElement_IsReported",
    file: loadingFile,
    code: 'export default function Loading() {\n  return <div className="grid gap-section" />;\n}',
    expected: [routeMarkup],
  },
  {
    title: "nextConfig_FeatureRendersAnHtmlElement_IsAllowed",
    file: componentFile,
    code: 'export function UploadPanel() {\n  return <div className="grid gap-section" />;\n}',
    expected: [],
  },
  {
    title: "nextConfig_RootLayoutAndGlobalErrorRenderHtmlAndBody_IsAllowed",
    file: "src/app/global-error.tsx",
    code: 'import { ApplicationFailure } from "@/shared/layout/application-failure";\nexport default function GlobalError() {\n  return (\n    <html lang="en">\n      <body>\n        <ApplicationFailure />\n      </body>\n    </html>\n  );\n}',
    expected: [],
  },
  {
    title: "nextConfig_RootLayoutRendersMoreThanHtmlAndBody_IsReported",
    file: "src/app/layout.tsx",
    code: 'export default function RootLayout() {\n  return (\n    <html lang="en">\n      <body>\n        <main />\n      </body>\n    </html>\n  );\n}',
    expected: [documentMarkup],
  },
  {
    title: "nextConfig_GlobalErrorRendersMoreThanHtmlAndBody_IsReported",
    file: "src/app/global-error.tsx",
    code: 'export default function GlobalError() {\n  return (\n    <html lang="en">\n      <body>\n        <h1>Failure</h1>\n      </body>\n    </html>\n  );\n}',
    expected: [documentMarkup],
  },
  {
    title: "nextConfig_StringHref_IsAllowed",
    file: componentFile,
    code: 'import Link from "next/link";\nexport function Pager({ page }: { page: number }) {\n  return <Link href={`/platform/attachments?page=${page}`}>Next</Link>;\n}',
    expected: [],
  },
  {
    title: "nextConfig_ObjectHref_IsReported",
    file: componentFile,
    code: 'import Link from "next/link";\nexport function Pager({ page }: { page: number }) {\n  return <Link href={{ pathname: "/platform/attachments", query: { page } }}>Next</Link>;\n}',
    expected: [objectHref],
  },
  {
    title: "nextConfig_WrappedObjectHref_IsReported",
    file: componentFile,
    code: [
      'import Link from "next/link";',
      "export function Pager({ open }: { open: boolean }) {",
      "  return (",
      "    <>",
      '      <Link href={{ pathname: "/a" } as Route}>A</Link>',
      '      <Link href={{ pathname: "/a" } satisfies UrlObject}>B</Link>',
      '      <Link href={open ? { pathname: "/a" } : "/b"}>C</Link>',
      '      <Link href={open && { pathname: "/a" }}>D</Link>',
      '      <Link href={{ pathname: "/a" } as unknown as Route}>E</Link>',
      '      <Link href={open ? (next ? { pathname: "/a" } : "/b") : "/c"}>F</Link>',
      "    </>",
      "  );",
      "}",
    ].join("\n"),
    expected: [objectHref, objectHref, objectHref, objectHref, objectHref, objectHref],
  },
  {
    title: "nextConfig_HrefBuiltFromAnObjectArgument_IsAllowed",
    file: componentFile,
    code: [
      'import Link from "next/link";',
      "export function Pager({ open, page, query }: { open: boolean; page: number; query?: Record<string, string> }) {",
      "  return (",
      "    <>",
      '      <Link href={open ? pageHref({ page }) : "/b"}>A</Link>',
      "      <Link href={pageHref(query ?? {})}>B</Link>",
      "      <Link href={pageHref({ ...(query || {}), page: String(page) })}>C</Link>",
      "      <Link href={pageHref(open ? { page: 1 } : { page: 2 })}>D</Link>",
      "      <Link href={`/platform/attachments?${new URLSearchParams(query ?? {})}`}>E</Link>",
      "    </>",
      "  );",
      "}",
    ].join("\n"),
    expected: [],
  },
  {
    title: "nextConfig_ObjectHrefInARoute_IsReported",
    file: pageFile,
    code: 'import Link from "next/link";\nexport default function Page() {\n  return <Link href={{ pathname: "/platform/attachments" }}>Attachments</Link>;\n}',
    expected: [objectHref],
  },
  {
    title: "nextConfig_TypeRoleOnAPlainElementInTheRootLayout_IsAllowed",
    file: "src/app/layout.tsx",
    code: 'export default function RootLayout() {\n  return (\n    <html lang="en">\n      <body className="text-body" />\n    </html>\n  );\n}',
    expected: [],
  },
  {
    title: "nextConfig_TypeRoleOnAComponentInAFeature_IsReported",
    file: componentFile,
    code: 'import { Badge } from "@dewiride/erp-ui/components/ui/badge";\nexport function Status() {\n  return <Badge className="text-caption">Stored</Badge>;\n}',
    expected: [typeRole],
  },
  {
    title: "nextConfig_TypeRoleOnAComponentInTheRootLayout_IsReported",
    file: "src/app/layout.tsx",
    code: 'import { Toaster } from "@dewiride/erp-ui/components/ui/sonner";\nexport default function RootLayout() {\n  return (\n    <html lang="en">\n      <body>\n        <Toaster className="text-caption" />\n      </body>\n    </html>\n  );\n}',
    expected: [typeRole],
  },
  {
    title: "nextConfig_TypeRoleOnAComponentInARoute_IsReported",
    file: pageFile,
    code: 'import { AttachmentsOverview } from "@/features/platform/attachments";\nexport default function Page() {\n  return <AttachmentsOverview className="text-heading" />;\n}',
    expected: [typeRole],
  },
  {
    title: "reactLibraryConfig_PackageImportsItsOwnCode_IsAllowed",
    preset: "ui",
    file: "src/components/upload/file-drop-zone.tsx",
    code: 'import { cn } from "@dewiride/erp-ui/lib/utils";\nimport { Button } from "../ui/button";\nimport { hooks } from "@dewiride/erp-webhooks";',
    expected: [],
  },
  {
    title: "reactLibraryConfig_PackageImportsTheWebApp_IsReported",
    preset: "ui",
    file: "src/components/upload/file-drop-zone.tsx",
    code: 'import { AppShell } from "@dewiride/erp-web/src/shared/layout/app-shell";\nimport { web } from "@dewiride/erp-web";\nimport { navigation } from "../../../../../apps/web/src/features/registry";',
    expected: [webAppFromPackage, webAppFromPackage, webAppFromPackage],
  },
];

const linters = new Map(Object.entries(presets).map(([key, { root }]) => [key, new Linter({ cwd: root })]));

function restrictedMessages(presetKey, file, code) {
  const { config, root } = presets[presetKey];
  const messages = linters.get(presetKey).verify(code, config, { filename: join(root, file) });
  const unlinted = messages.filter((message) => message.fatal === true || message.ruleId === null);

  assert.deepEqual(unlinted, [], `${file} must parse and match a configuration object`);

  return messages.filter((message) => restrictedRules.has(message.ruleId));
}

for (const { title, preset = "web", file, code, expected } of cases) {
  test(title, () => {
    const actual = restrictedMessages(preset, file, code);

    assert.equal(
      actual.length,
      expected.length,
      actual.map((message) => `${message.line}: ${message.ruleId} ${message.message}`).join("\n"),
    );
    expected.forEach(([ruleId, message], index) => {
      assert.equal(actual[index].ruleId, ruleId);
      assert.match(actual[index].message, message);
    });
  });
}
