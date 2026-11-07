import type { ScaffoldModule } from "./module.ts";
import { indentationAt, insertAt, insertElement, listAfter, nextLineStart, ordinal, preservingShape, sameText, snippet } from "./text.ts";

export type RegisteredModuleName = { readonly domain: string; readonly module: string };

export const solutionFilterPath = "..\\Dewiride.Erp.slnx";

const aliasDirective = /^using ErpModules = Dewiride\.Erp\.Modules;$/m;
const modulesAllList = /\bAll\s*\{\s*get;\s*\}\s*=\s*\[/;
const aliasedModule = /^new ErpModules\.([A-Z][A-Za-z0-9]*)\.([A-Z][A-Za-z0-9]*)\.([A-Z][A-Za-z0-9]*)Module\(\)$/;
const projectReference = /^([ \t]*)<ProjectReference Include="([^"]+)"\s*\/>[ \t]*$/gm;
const solutionFolder = /^([ \t]*)<Folder Name="([^"]+)"/gm;
const solutionProject = /<Project Path="([^"]+)"/g;
const solutionEnd = /^[ \t]*<\/Solution>/m;
const filterTable = /^\| Filter \| Contents \|\n\|[-| ]+\|\n/m;
const filterRow = /^\| `([^`]+)\.slnf` \|/;

export function modulesAllOf(text: string): { readonly modules: RegisteredModuleName[]; readonly unreadable: string[] } {
  const source = text.replaceAll("\r\n", "\n");
  const list = listAfter(source, modulesAllList, "Modules.All collection");
  const modules: RegisteredModuleName[] = [];
  const unreadable: string[] = [];
  for (const span of list.elements) {
    const entry = source.slice(span.start, span.end).replace(/,$/, "").trim();
    const match = aliasedModule.exec(entry);
    if (match !== null && match[3] === match[2]) modules.push({ domain: match[1] as string, module: match[2] as string });
    else unreadable.push(entry);
  }

  return { modules, unreadable };
}

// Every module is written through the one alias, so two domains may each hold a module of one name.
export function registerInModulesAll(text: string, module: ScaffoldModule): string {
  return preservingShape(text, (source) => {
    if (!aliasDirective.test(source)) throw new Error("it does not alias Dewiride.Erp.Modules as ErpModules, the form every entry of Modules.All uses");
    const named = new RegExp(`\\b(?:ErpModules|Dewiride\\.Erp\\.Modules)\\.${module.domain.pascal}\\.${module.module.pascal}\\b`);
    if (named.test(source)) throw new Error(`it already registers the module ${module.domain.pascal}/${module.module.pascal}`);
    const list = listAfter(source, modulesAllList, "Modules.All collection");
    return insertElement(source, list, list.elements.at(-1), `new ${module.moduleClass}()`);
  });
}

export function projectReferencesOf(text: string): string[] {
  return [...text.replaceAll("\r\n", "\n").matchAll(projectReference)].map((match) => match[2] as string);
}

export function compositionReference(module: ScaffoldModule): string {
  return `../../../${module.moduleProject}`;
}

export function registerInComposition(text: string, module: ScaffoldModule): string {
  return preservingShape(text, (source) => {
    const references = [...source.matchAll(projectReference)].map((match) => ({ index: match.index, indentation: match[1] as string, include: match[2] as string }));
    const last = references.at(-1);
    if (last === undefined) throw new Error('it holds no <ProjectReference Include="…" /> to add the module\'s beside');
    const separator = separatorOf(references.map(({ include }) => include), "/");
    const include = compositionReference(module).replaceAll("/", separator);
    if (references.some((reference) => sameText(slashed(reference.include), slashed(include)))) throw new Error(`it already references ${include}`);
    const next = references.find((reference) => ordinal(slashed(reference.include), slashed(include)) > 0);
    const line = `${(next ?? last).indentation}<ProjectReference Include="${include}" />\n`;
    return insertAt(source, next === undefined ? nextLineStart(source, last.index) : next.index, line);
  });
}

export function solutionProjectsOf(text: string): string[] {
  return [...text.matchAll(solutionProject)].map((match) => slashed(match[1] as string));
}

export function solutionFoldersOf(text: string): string[] {
  return [...text.replaceAll("\r\n", "\n").matchAll(solutionFolder)].map((match) => match[2] as string);
}

// The module's two solution folders go where their names sort among the folders, which the solution keeps in name order.
export function registerInSolution(text: string, module: ScaffoldModule): string {
  return preservingShape(text, (source) => {
    const folders = [...source.matchAll(solutionFolder)].map((match) => ({ index: match.index, indentation: match[1] as string, name: match[2] as string }));
    const names = [`/${module.folder}/`, `/${module.folder}/Tests/`];
    const listed = solutionProjectsOf(source);
    const taken = [...folders.map(({ name }) => name).filter((name) => names.some((own) => sameText(own, name))), ...module.projects.filter((path) => listed.some((other) => sameText(other, path)))];
    if (taken.length > 0) throw new Error(`it already lists ${taken.join(", ")}`);
    const end = solutionEnd.exec(source);
    if (end === null) throw new Error("it holds no </Solution> to close the module's folders");
    const next = folders.find((folder) => ordinal(folder.name, names[0] as string) > 0);
    const folderIndentation = (next ?? folders[0])?.indentation ?? `${indentationAt(source, end.index)}  `;
    const projectIndentation = /^([ \t]*)<Project /m.exec(source)?.[1] ?? `${folderIndentation}  `;
    const separator = separatorOf([...source.matchAll(solutionProject)].map((match) => match[1] as string), "/");
    const [contracts, implementation, unitTests, integrationTests] = module.projects;
    const folder = (name: string, projects: readonly string[]): string[] => [
      `${folderIndentation}<Folder Name="${name}">`,
      ...projects.map((project) => `${projectIndentation}<Project Path="${project.replaceAll("/", separator)}" />`),
      `${folderIndentation}</Folder>`,
    ];
    const block = [...folder(names[0] as string, [contracts, implementation]), ...folder(names[1] as string, [unitTests, integrationTests])].join("\n");
    return insertAt(source, next === undefined ? end.index : next.index, `${block}\n`);
  });
}

export function filterProjectsOf(text: string): string[] {
  const document: unknown = JSON.parse(text.replace(/^\uFEFF/, ""));
  return [...filterProjectList(document)];
}

export function registerInDomainFilter(text: string, module: ScaffoldModule): string {
  return preservingShape(text, (source) =>
    editJson(source, (document) => {
      const projects = filterProjectList(document);
      const separator = separatorOf(projects, "\\");
      const additions = module.projects.map((path) => path.replaceAll("/", separator));
      const taken = additions.filter((path) => projects.some((listed) => sameText(listed, path)));
      if (taken.length > 0) throw new Error(`it already lists ${taken.join(", ")}`);
      for (const path of additions) {
        const at = projects.findIndex((listed) => ordinal(listed, path) > 0);
        projects.splice(at === -1 ? projects.length : at, 0, path);
      }
    }),
  );
}

// A domain's filter holds every project outside Modules/ and the projects of the domain's own modules.
export function domainFilter(solutionText: string, module: ScaffoldModule): string {
  const shared = solutionProjectsOf(solutionText).filter((path) => !path.startsWith("Modules/"));
  if (shared.length === 0) throw new Error("the solution lists no project outside Modules/ for the domain's filter");
  const projects = [...shared, ...module.projects].map((path) => path.replaceAll("/", "\\")).sort(ordinal);
  return `${JSON.stringify({ solution: { path: solutionFilterPath, projects } }, null, 2)}\n`;
}

export function filterNamesOf(text: string): string[] {
  const source = text.replaceAll("\r\n", "\n");
  const table = filterTable.exec(source);
  if (table === null) return [];
  return rowsAfter(source, table.index + table[0].length).flatMap((row) => {
    const name = filterRow.exec(row.text)?.[1];
    return name === undefined ? [] : [name];
  });
}

export function registerFilterInReadme(text: string, module: ScaffoldModule): string {
  return preservingShape(text, (source) => {
    const table = filterTable.exec(source);
    if (table === null) throw new Error("it holds no | Filter | Contents | table to add the domain's filter to");
    const rows = rowsAfter(source, table.index + table[0].length).map((row) => ({ ...row, name: filterRow.exec(row.text)?.[1] ?? "" }));
    if (rows.some(({ name }) => sameText(name, module.domain.pascal))) throw new Error(`it already lists ${module.domain.pascal}.slnf`);
    const next = rows.find(({ name }) => ordinal(name, module.domain.pascal) > 0);
    const at = next?.index ?? (rows.length === 0 ? table.index + table[0].length : nextLineStart(source, (rows.at(-1) as { index: number }).index));
    return insertAt(source, at, snippet("solutions-row.md", module.tokens));
  });
}

export function filterProjectList(document: unknown): string[] {
  const projects = (document as { solution?: { projects?: unknown } } | null)?.solution?.projects;
  if (!Array.isArray(projects) || !projects.every((path) => typeof path === "string")) throw new Error('it does not hold { "solution": { "projects": [...] } }');
  return projects as string[];
}

// The file is rewritten as JSON.stringify writes it, so it must already be written that way: then the module's lines are
// the only change.
export function editJson(source: string, change: (document: unknown) => void): string {
  let document: unknown;
  try {
    document = JSON.parse(source);
  } catch (error) {
    throw new Error(`it is not valid JSON: ${(error as Error).message}`);
  }

  const ending = source.endsWith("\n") ? "\n" : "";
  const indentation = ["  ", "    ", "\t"].find((candidate) => JSON.stringify(document, null, candidate) + ending === source);
  if (indentation === undefined) throw new Error("it is not laid out as JSON.stringify writes it, so adding to it would rewrite every line; lay it out with two-space indentation first");
  change(document);
  return JSON.stringify(document, null, indentation) + ending;
}

export function rowsAfter(source: string, start: number): { readonly index: number; readonly text: string }[] {
  const rows: { index: number; text: string }[] = [];
  for (let index = start; index < source.length && source[index] === "|"; index = nextLineStart(source, index)) {
    rows.push({ index, text: source.slice(index, nextLineStart(source, index)).replace(/\n$/, "") });
  }

  return rows;
}

function slashed(path: string): string {
  return path.replaceAll("\\", "/");
}

function separatorOf(paths: readonly string[], fallback: "/" | "\\"): "/" | "\\" {
  const backslashed = paths.filter((path) => path.includes("\\")).length;
  const slashedPaths = paths.filter((path) => path.includes("/")).length;
  if (backslashed === slashedPaths) return fallback;
  return backslashed > slashedPaths ? "\\" : "/";
}
