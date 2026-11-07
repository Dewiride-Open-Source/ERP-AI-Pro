import { existsSync, readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";

import {
  filterNamesOf,
  filterProjectsOf,
  modulesAllOf,
  projectReferencesOf,
  type RegisteredModuleName,
  solutionFilterPath,
  solutionProjectsOf,
} from "./registrations/backend.ts";
import { moduleProjects } from "./registrations/module.ts";
import { domainFilterPath, paths } from "./registrations/paths.ts";
import { flagRowsOf, flagSeedsOf, routedModulesOf, seedLabels } from "./registrations/repository.ts";
import { ordinal } from "./registrations/text.ts";
import { areaOf, changeKeysOf, recordTypeKeysOf, type RegisteredNavigation, registeredNavigationOf, registryOf } from "./registrations/web.ts";
import { filesUnder, navigationOf, permissionsOf, productSources, recordedChangeKeysOf } from "./sources.ts";

type Parse = <T>(path: string, parse: (text: string) => T, fallback: T) => T;

// Every place a module is registered by hand, read from the repository as it stands, so a registration the scaffolder makes
// and one made by hand are held to the same rules.
export function registrationAudit(root: string): string[] {
  const problems: string[] = [];
  const parsed: Parse = (path, parse, fallback) => {
    const file = join(root, path);
    if (!existsSync(file)) {
      problems.push(`${path} does not exist`);
      return fallback;
    }

    try {
      return parse(readFileSync(file, "utf8"));
    } catch (error) {
      problems.push(`${path}: ${(error as Error).message}`);
      return fallback;
    }
  };

  const { modules, unreadable } = parsed(paths.modulesAll, modulesAllOf, { modules: [], unreadable: [] });
  for (const entry of unreadable) problems.push(`${paths.modulesAll}: ${entry} is not written new ErpModules.<Domain>.<Module>.<Module>Module()`);
  if (modules.length === 0) return [...problems, `${paths.modulesAll} registers no module that can be read`];
  backendAudit(root, parsed, modules, problems);
  repositoryAudit(parsed, modules, problems);
  navigationAudit(root, parsed, problems);
  changeKeyAudit(root, parsed, problems);
  return problems;
}

function projectsOf(name: RegisteredModuleName): readonly string[] {
  return moduleProjects(name.domain, name.module);
}

function backendAudit(root: string, parsed: Parse, modules: readonly RegisteredModuleName[], problems: string[]): void {
  const owned = new Set(modules.flatMap(projectsOf));
  const references = parsed(paths.composition, projectReferencesOf, []).map((include) => include.replaceAll("\\", "/").replace(/^(?:\.\.\/)+/, ""));
  const solution = parsed(paths.solution, solutionProjectsOf, []);
  for (const name of modules) {
    const [, implementation] = moduleProjects(name.domain, name.module);
    if (!references.includes(implementation)) problems.push(`${paths.composition} does not reference ${implementation}`);
    for (const project of projectsOf(name)) if (!solution.includes(project)) problems.push(`${paths.solution} does not list ${project}`);
  }

  for (const reference of references) if (reference.startsWith("Modules/") && !owned.has(reference)) problems.push(`${paths.composition} references ${reference}, which no module of Modules.All owns`);
  for (const project of solution) if (project.startsWith("Modules/") && !owned.has(project)) problems.push(`${paths.solution} lists ${project}, which no module of Modules.All owns`);
  filterAudit(root, parsed, modules, solution, problems);
}

// Each domain's filter holds every project outside Modules/ and the projects of the domain's own modules, in ordinal order,
// and the filters' README names every filter.
function filterAudit(root: string, parsed: Parse, modules: readonly RegisteredModuleName[], solution: readonly string[], problems: string[]): void {
  const shared = solution.filter((project) => !project.startsWith("Modules/"));
  const domains = [...new Set(modules.map(({ domain }) => domain))];
  for (const domain of domains) {
    const path = domainFilterPath(domain);
    const filter = parsed(path, (text) => ({ solution: (JSON.parse(text.replace(/^\uFEFF/, "")) as { solution?: { path?: unknown } }).solution?.path, projects: filterProjectsOf(text) }), undefined);
    if (filter === undefined) continue;
    const expected = [...shared, ...modules.filter((name) => name.domain === domain).flatMap(projectsOf)].map((project) => project.replaceAll("/", "\\")).sort(ordinal);
    if (filter.solution !== solutionFilterPath) problems.push(`${path} does not name ${solutionFilterPath}`);
    for (const project of expected) if (!filter.projects.includes(project)) problems.push(`${path} does not list ${project}`);
    for (const project of filter.projects) if (!expected.includes(project)) problems.push(`${path} lists ${project}, which is neither outside Modules/ nor a project of a ${domain} module`);
    if (filter.projects.join("\n") !== [...filter.projects].sort(ordinal).join("\n")) problems.push(`${path} does not keep its projects in ordinal order`);
  }

  const folder = join(root, paths.filters);
  const filters = existsSync(folder) ? readdirSync(folder).filter((name) => name.endsWith(".slnf")).map((name) => name.slice(0, -".slnf".length)) : [];
  for (const filter of filters) if (!domains.includes(filter)) problems.push(`${domainFilterPath(filter)} filters the domain ${filter}, which holds no module of Modules.All`);
  const rows = parsed(paths.filtersReadme, filterNamesOf, []);
  for (const filter of filters) if (!rows.includes(filter)) problems.push(`${paths.filtersReadme} has no row for ${filter}.slnf`);
  for (const row of rows) if (!filters.includes(row)) problems.push(`${paths.filtersReadme} lists ${row}.slnf, which does not exist`);
}

function repositoryAudit(parsed: Parse, modules: readonly RegisteredModuleName[], problems: string[]): void {
  const flags = new Set(modules.map(({ domain, module }) => `Erp.Modules.${domain}.${module}`));
  const owned = (id: string): boolean => flags.has(id.split(".").slice(0, 4).join("."));
  const seeds = parsed(paths.flags, flagSeedsOf, []);
  const rows = parsed(paths.configuration, flagRowsOf, []);
  const routing = parsed(paths.routing, routedModulesOf, { rows: [], routeTables: [] });
  for (const flag of flags) {
    const seed = seeds.find(({ id }) => id === flag);
    if (seed === undefined) problems.push(`${paths.flags} does not seed ${flag}`);
    else for (const label of seedLabels) if (typeof seed.enabled?.[label] !== "boolean") problems.push(`${paths.flags} seeds ${flag} without a value for ${label}`);
    if (!rows.includes(flag)) problems.push(`${paths.configuration} has no row for ${flag}`);
  }

  for (const { id } of seeds) if (id.startsWith("Erp.Modules.") && !owned(id)) problems.push(`${paths.flags} seeds ${id}, which belongs to no module of Modules.All`);
  for (const row of rows) if (!owned(row)) problems.push(`${paths.configuration} describes ${row}, which belongs to no module of Modules.All`);
  const names = modules.map(({ domain, module }) => `${domain}/${module}`);
  for (const name of names) if (!routing.rows.includes(name)) problems.push(`${paths.routing} has no row for ${name.replace("/", " / ")} in its Modules table`);
  for (const row of routing.rows) if (!names.includes(row)) problems.push(`${paths.routing} lists ${row.replace("/", " / ")}, which is not a module of Modules.All`);
  for (const table of routing.routeTables) if (!names.includes(table)) problems.push(`${paths.routing} has a route table for ${table.replace("/", " / ")}, which is not a module of Modules.All`);
}

// The e2e list of navigation entries is written by hand, so it is held to the registry and the manifests it mirrors: each
// area's entries together, in the registry's order, with the area's title and whether the Employee role sees the entry.
function navigationAudit(root: string, parsed: Parse, problems: string[]): void {
  for (const { path, text } of filesUnder(root, paths.features, (name) => name.endsWith("-area.ts"))) {
    const [domain, folder] = path.split("/").slice(-3) as [string, string];
    if (folder !== "_shared") continue;
    const camel = domain.replace(/-([a-z0-9])/g, (_, letter: string) => letter.toUpperCase());
    if (!path.endsWith(`/${domain}/_shared/${domain}-area.ts`)) problems.push(`${path} is not named after its domain folder, ${domain}-area.ts`);
    else if (areaOf(text)?.name !== `${camel}Area`) problems.push(`${path} does not export ${camel}Area = { id, title } as const`);
  }

  const { manifests, areas } = navigationOf(root);
  const permissions = permissionsOf(productSources(root));
  const expected: RegisteredNavigation[] = [];
  for (const entry of parsed(paths.registry, registryOf, [])) {
    const manifestFile = `${paths.features}/${entry.from.replace(/^@\/features\//, "")}/nav.ts`;
    const manifest = manifests.find(({ name, file }) => name === entry.name && file === manifestFile);
    if (manifest === undefined) {
      problems.push(`${paths.registry} imports ${entry.name} from ${entry.from || "nowhere"}, but ${manifestFile} does not declare it`);
      continue;
    }

    const area = areas.find(({ name }) => name === manifest.area);
    const permission = permissions.find(({ name }) => name === manifest.permission);
    if (area === undefined) problems.push(`${manifest.file}: ${manifest.name} names the area ${manifest.area}, which no area file exports`);
    if (permission === undefined) problems.push(`${manifest.file}: ${manifest.name} needs ${manifest.permission}, which no module declares`);
    expected.push({ area: area?.title ?? "", title: manifest.title, href: manifest.basePath, description: manifest.description, employees: permission?.employees ?? false });
  }

  const order: string[] = [];
  for (const { area } of expected) {
    if (order.at(-1) === area) continue;
    if (order.includes(area)) problems.push(`${paths.registry} splits the entries of the area ${area}`);
    order.push(area);
  }

  const listed = parsed(paths.navigationFixture, registeredNavigationOf, undefined);
  if (listed === undefined) return;
  for (let index = 0; index < Math.max(expected.length, listed.length); index += 1) {
    const want = expected[index];
    const have = listed[index];
    if (JSON.stringify(want) !== JSON.stringify(have)) {
      problems.push(`${paths.navigationFixture}: registeredNavigation[${index}] is ${JSON.stringify(have) ?? "missing"}, but the registry's entry ${index} is ${JSON.stringify(want) ?? "missing"}`);
    }
  }
}

function changeKeyAudit(root: string, parsed: Parse, problems: string[]): void {
  const recorded = recordedChangeKeysOf(productSources(root));
  for (const { argument, file, key } of recorded) if (key === undefined) problems.push(`${file}: RecordChangesAs(${argument}) is not given its change key as a string literal`);
  const keys = new Set(recorded.flatMap(({ key }) => (key === undefined ? [] : [key])));
  const filtered = parsed(paths.changeKeys, changeKeysOf, []);
  const labelled = parsed(paths.recordTypes, recordTypeKeysOf, []);
  for (const key of keys) {
    if (!filtered.includes(key)) problems.push(`${paths.changeKeys}: changeKeys does not list ${key}, so the audit trail cannot filter by it`);
    if (!labelled.includes(key)) problems.push(`${paths.recordTypes}: recordTypes does not name ${key}, so the audit trail shows the bare key`);
  }

  for (const key of filtered) if (!keys.has(key)) problems.push(`${paths.changeKeys}: changeKeys lists ${key}, which no entity records`);
  for (const key of labelled) if (!keys.has(key)) problems.push(`${paths.recordTypes}: recordTypes names ${key}, which no entity records`);
}
