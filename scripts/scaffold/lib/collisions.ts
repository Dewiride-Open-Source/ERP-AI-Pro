import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";

import { discoverContexts } from "../../ef/lib/contexts.ts";
import { formsOf, type ScaffoldInput } from "./names.ts";
import type { ScaffoldModule } from "./registrations/module.ts";
import { areaFilePath, paths } from "./registrations/paths.ts";
import { sameText } from "./registrations/text.ts";
import { areaOf } from "./registrations/web.ts";
import { featureFlagsOf, folderNames, iconExportsOf, navigationOf, permissionsOf, productSources, routePrefixesOf, schemasOf, shellTitlesOf } from "./sources.ts";

export type DomainState = { readonly newDomain: boolean; readonly areaTitle: string; readonly problems: readonly string[] };

// A domain exists once it holds a module; its area in the navigation then exists too and keeps its title, while a new
// domain's area takes the title the command is given.
export function domainStateOf(root: string, input: ScaffoldInput): DomainState {
  const problems: string[] = [];
  const domains = folderNames(root, paths.backendModules);
  const variant = domains.find((name) => name !== input.domain && sameText(name, input.domain));
  if (variant !== undefined) problems.push(`the domain ${input.domain} differs from the domain ${variant} only in letter case`);
  const newDomain = !domains.includes(input.domain) || folderNames(root, `${paths.backendModules}/${input.domain}`).length === 0;
  if (newDomain) {
    if (input.areaTitle === undefined) problems.push(`the domain ${input.domain} is new, so --area-title must give its area's title in the navigation`);
    return { newDomain, areaTitle: input.areaTitle ?? "", problems };
  }

  if (input.areaTitle !== undefined) problems.push(`the domain ${input.domain} exists and its area keeps its title, so leave out --area-title`);
  const forms = formsOf(input.domain);
  const file = areaFilePath(forms.kebab);
  const area = existsSync(join(root, file)) ? areaOf(readFileSync(join(root, file), "utf8")) : undefined;
  if (area?.name !== `${forms.camel}Area`) problems.push(`the domain ${input.domain} has no area in the navigation: ${file} does not export ${forms.camel}Area`);
  return { newDomain, areaTitle: area?.title ?? "", problems };
}

export function collisionProblems(root: string, module: ScaffoldModule): string[] {
  return [...folderProblems(root, module), ...backendProblems(root, module), ...contractProblems(root, module), ...webProblems(root, module)];
}

function folderProblems(root: string, module: ScaffoldModule): string[] {
  const problems: string[] = [];
  const domainFolder = `${paths.backendModules}/${module.domain.pascal}`;
  const variant = folderNames(root, domainFolder).find((name) => sameText(name, module.module.pascal));
  if (variant !== undefined && variant !== module.module.pascal) problems.push(`the module ${module.module.pascal} differs from the module ${module.domain.pascal}/${variant} only in letter case`);
  const scope = module.newDomain ? module.domain.kebab : `${module.domain.kebab}/${module.module.kebab}`;
  const folders = [
    module.newDomain ? undefined : `${domainFolder}/${module.module.pascal}`,
    `${paths.features}/${scope}`,
    `${paths.appRoutes}/${scope}`,
    `${paths.e2e}/pages/${scope}`,
    `${paths.e2e}/tests/${scope}`,
  ];
  for (const folder of folders) if (folder !== undefined && existsSync(join(root, folder))) problems.push(`${folder} already exists`);
  return problems;
}

function backendProblems(root: string, module: ScaffoldModule): string[] {
  const problems: string[] = [];
  const sources = productSources(root);
  for (const { schema, file } of schemasOf(sources)) if (sameText(schema, module.schema)) problems.push(`the schema ${module.schema} is already used (${file})`);
  for (const { prefix, file } of routePrefixesOf(sources)) if (sameText(prefix, module.routePrefix)) problems.push(`the route prefix ${module.routePrefix} is already used (${file})`);
  for (const { flag, file } of featureFlagsOf(sources)) if (sameText(flag, module.featureFlag)) problems.push(`the feature flag ${module.featureFlag} is already declared (${file})`);
  const sentences = [module.input.readSentence, module.input.createSentence];
  if (sameText(module.input.readSentence, module.input.createSentence)) problems.push("the read and create permissions are described by the same sentence");
  for (const permission of permissionsOf(sources)) {
    for (const sentence of sentences) if (sameText(permission.description, sentence)) problems.push(`the permission ${permission.name} already says "${permission.description}"`);
  }

  try {
    const taken = discoverContexts(join(root, "backend")).find((context) => context.key === module.contextKey);
    if (taken !== undefined) problems.push(`the migration key ${module.contextKey} already belongs to ${taken.contextName} (${taken.project})`);
  } catch (error) {
    problems.push(`the database contexts cannot be listed: ${(error as Error).message}`);
  }

  return problems;
}

function contractProblems(root: string, module: ScaffoldModule): string[] {
  const problems: string[] = [];
  const flags = join(root, paths.flags);
  if (existsSync(flags)) {
    const flagIds = [...readFileSync(flags, "utf8").matchAll(/"id":\s*"([^"]*)"/g)].map((match) => match[1] as string);
    for (const id of flagIds) if (sameText(id, module.featureFlag) || id.toUpperCase().startsWith(`${module.featureFlag.toUpperCase()}.`)) problems.push(`${paths.flags} already seeds ${id}`);
  }

  const contract = join(root, paths.openApi);
  if (!existsSync(contract)) return [...problems, `${paths.openApi} does not exist, so the API's routes and models cannot be checked`];
  const document = JSON.parse(readFileSync(contract, "utf8")) as { paths?: Record<string, unknown>; components?: { schemas?: Record<string, unknown> } };
  const prefix = `/api${module.routePrefix}`;
  for (const route of Object.keys(document.paths ?? {})) {
    if (sameText(route, prefix) || route.toUpperCase().startsWith(`${prefix.toUpperCase()}/`)) problems.push(`the API already answers ${route} (${paths.openApi})`);
  }

  for (const model of Object.keys(document.components?.schemas ?? {})) {
    if (module.apiModels.some((name) => sameText(name, model))) problems.push(`the API already has a model named ${model} (${paths.openApi})`);
  }

  return problems;
}

function webProblems(root: string, module: ScaffoldModule): string[] {
  const problems: string[] = [];
  const { manifests, areas } = navigationOf(root);
  for (const title of shellTitlesOf(root)) if (sameText(title, module.input.title)) problems.push(`the navigation title "${module.input.title}" is the shell's own ${title} link`);
  for (const manifest of manifests) {
    if (sameText(manifest.title, module.input.title)) problems.push(`the navigation title "${manifest.title}" is already used (${manifest.file})`);
    if (manifest.id === module.navigationId) problems.push(`the navigation id ${manifest.id} is already used (${manifest.file})`);
    if (manifest.name === module.navigationExport) problems.push(`the navigation export ${manifest.name} already exists (${manifest.file}), and the registry imports every one by name`);
  }

  if (module.newDomain) {
    for (const area of areas) {
      if (sameText(area.title, module.areaTitle)) problems.push(`the area title "${area.title}" is already used (${area.file})`);
      if (area.id === module.domain.kebab) problems.push(`the area id ${area.id} is already used (${area.file})`);
    }
  }

  const icons = iconExportsOf(root);
  if (icons === undefined) problems.push(`${paths.lucide} is missing, so the icon cannot be checked; run pnpm install in frontend first`);
  else if (!icons.has(module.input.icon)) problems.push(`lucide-react exports no icon named ${module.input.icon}`);
  return problems;
}
