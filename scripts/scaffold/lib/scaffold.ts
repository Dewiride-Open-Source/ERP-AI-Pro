import { spawnSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { dirname, join, posix, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

import { ChangeSet } from "./change-set.ts";
import { collisionProblems, domainStateOf } from "./collisions.ts";
import { completionProblems } from "./completion.ts";
import { nameProblems, type ScaffoldInput } from "./names.ts";
import { domainFilter, registerFilterInReadme, registerInComposition, registerInDomainFilter, registerInModulesAll, registerInSolution } from "./registrations/backend.ts";
import { describeModule, type ScaffoldModule } from "./registrations/module.ts";
import { domainFilterPath, paths } from "./registrations/paths.ts";
import { registerFlag, registerFlagRow, registerRoutes } from "./registrations/repository.ts";
import { registerChangeKey, registerInNavigationFixture, registerInRegistry, registerRecordType } from "./registrations/web.ts";
import { renderText, renderTree } from "./templates.ts";

export type PlanOptions = {
  readonly filesOnly: boolean;
  readonly environment: NodeJS.ProcessEnv;
  readonly templates?: string;
  readonly dotnetVersion?: (folder: string) => string | undefined;
};

export type Plan = {
  readonly module: ScaffoldModule | undefined;
  readonly changes: ChangeSet;
  readonly problems: readonly string[];
  readonly createdRoots: readonly string[];
};

export const defaultTemplates = resolve(dirname(fileURLToPath(import.meta.url)), "..", "templates");

// The template folders and where each renders; the area of the navigation exists once per domain.
const templateRoots = [
  { template: "backend", output: "backend/Modules/__Domain__/__Module__", newDomainOnly: false },
  { template: "web/feature", output: `${paths.features}/__domain-kebab__/__module-kebab__`, newDomainOnly: false },
  { template: "web/routes", output: `${paths.appRoutes}/__domain-kebab__/__module-kebab__`, newDomainOnly: false },
  { template: "web/area", output: `${paths.features}/__domain-kebab__/_shared`, newDomainOnly: true },
  { template: "e2e", output: paths.e2e, newDomainOnly: false },
] as const;

// What the completion rewrites besides the planned files, so a run starts only where git restore and git clean undo it all.
export const completionOutputs = [paths.openApi, paths.generatedClient, ":(glob)backend/**/packages.lock.json"] as const;

export function planModule(root: string, input: ScaffoldInput, options: PlanOptions): Plan {
  const changes = new ChangeSet(root);
  const named = nameProblems(input);
  if (named.length > 0) return { module: undefined, changes, problems: named, createdRoots: [] };

  const domain = domainStateOf(root, input);
  const module = describeModule(input, domain);
  const problems = [...domain.problems, ...collisionProblems(root, module)];
  for (const { template, output, newDomainOnly } of templateRoots) {
    if (newDomainOnly && !module.newDomain) continue;
    try {
      for (const file of renderTree(join(options.templates ?? defaultTemplates, template), join(root, renderText(output, module.tokens, output)), module.tokens)) {
        changes.create(file.output, file.contents);
      }
    } catch (error) {
      problems.push(`the templates in ${template} cannot be rendered: ${(error as Error).message}`);
    }
  }

  planRegistrations(root, module, changes, problems);
  const edited = changes.changes.filter(({ kind }) => kind === "edit").map(({ path }) => path);
  problems.push(...changes.problems, ...uncommittedProblems(root, [...edited, ...completionOutputs]));
  if (!options.filesOnly) problems.push(...completionProblems(root, options.environment, options.dotnetVersion));
  return { module, changes, problems, createdRoots: createdRootsOf(root, changes) };
}

function planRegistrations(root: string, module: ScaffoldModule, changes: ChangeSet, problems: string[]): void {
  changes.edit(paths.modulesAll, (text) => registerInModulesAll(text, module));
  changes.edit(paths.composition, (text) => registerInComposition(text, module));
  changes.edit(paths.solution, (text) => registerInSolution(text, module));
  if (module.newDomain) {
    try {
      changes.create(domainFilterPath(module.domain.pascal), domainFilter(readFileSync(join(root, paths.solution), "utf8"), module));
    } catch (error) {
      problems.push(`${domainFilterPath(module.domain.pascal)} cannot be written: ${(error as Error).message}`);
    }

    changes.edit(paths.filtersReadme, (text) => registerFilterInReadme(text, module));
  } else {
    changes.edit(domainFilterPath(module.domain.pascal), (text) => registerInDomainFilter(text, module));
  }

  changes.edit(paths.flags, (text) => registerFlag(text, module));
  changes.edit(paths.configuration, (text) => registerFlagRow(text, module));
  changes.edit(paths.routing, (text) => registerRoutes(text, module));
  changes.edit(paths.registry, (text) => registerInRegistry(text, module));
  changes.edit(paths.navigationFixture, (text) => registerInNavigationFixture(text, module));
  changes.edit(paths.changeKeys, (text) => registerChangeKey(text, module));
  changes.edit(paths.recordTypes, (text) => registerRecordType(text, module));
}

export function uncommittedProblems(root: string, pathspecs: readonly string[]): string[] {
  const status = spawnSync("git", ["status", "--porcelain=v1", "-z", "--untracked-files=all", "--", ...pathspecs], { cwd: root, encoding: "utf8", shell: false });
  if (status.error !== undefined || status.status !== 0) return [`git cannot tell whether the files this run changes are committed: ${(status.stderr ?? "").trim() || status.error?.message}`];
  const entries = status.stdout.split("\0").filter((entry) => entry !== "");
  const changed: string[] = [];
  for (let index = 0; index < entries.length; index += 1) {
    const entry = entries[index] as string;
    changed.push(entry.slice(3));
    if (entry[0] === "R" || entry[0] === "C") index += 1;
  }

  return changed.map((path) => `${path} has uncommitted changes; commit or stash them first, so git restore and git clean can undo this run`);
}

// The highest folder each created file brings into being, or the file itself in a folder that exists, so git clean removes
// what the run created and nothing else.
function createdRootsOf(root: string, changes: ChangeSet): string[] {
  const roots = new Set<string>();
  for (const { path, kind } of changes.changes) {
    if (kind !== "create") continue;
    let created = join(root, path);
    while (!existsSync(dirname(created))) created = dirname(created);
    roots.add(relative(root, created).split(sep).join(posix.sep));
  }

  return [...roots].sort();
}
