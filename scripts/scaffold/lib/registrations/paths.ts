export const paths = {
  backendModules: "backend/Modules",
  backendBuildingBlocks: "backend/BuildingBlocks",
  modulesAll: "backend/Hosts/Composition/Dewiride.Erp.Host.Composition/Modules.cs",
  composition: "backend/Hosts/Composition/Dewiride.Erp.Host.Composition/Dewiride.Erp.Host.Composition.csproj",
  solution: "backend/Dewiride.Erp.slnx",
  filters: "backend/solutions",
  filtersReadme: "backend/solutions/README.md",
  flags: "infra/appconfig/feature-flags.json",
  configuration: "docs/configuration.md",
  routing: "docs/routing.md",
  openApi: "docs/openapi/erp.json",
  features: "frontend/apps/web/src/features",
  appRoutes: "frontend/apps/web/src/app/(app)",
  registry: "frontend/apps/web/src/features/registry.ts",
  changeKeys: "frontend/apps/web/src/features/platform/audit/changes/lists/changes.list.ts",
  recordTypes: "frontend/apps/web/src/features/platform/audit/changes/components/change-text.ts",
  navigationTrail: "frontend/apps/web/src/shared/layout/navigation/navigation-trail.ts",
  lucide: "frontend/apps/web/node_modules/lucide-react",
  e2e: "frontend/e2e",
  navigationFixture: "frontend/e2e/fixtures/navigation.ts",
  generatedClient: "frontend/packages/api-client/src/generated",
  prettier: "frontend/node_modules/prettier/bin/prettier.cjs",
} as const;

export function domainFilterPath(domain: string): string {
  return `${paths.filters}/${domain}.slnf`;
}

export function areaFilePath(domainKebab: string): string {
  return `${paths.features}/${domainKebab}/_shared/${domainKebab}-area.ts`;
}
