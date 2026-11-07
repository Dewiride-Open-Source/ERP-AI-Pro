import type { ScaffoldModule } from "./module.ts";
import { indentationAt, insertAt, insertElement, type ListLiteral, listAfter, ordinal, preservingShape, type Span, stringLiteral, stringValue } from "./text.ts";

export type RegistryEntry = { readonly name: string; readonly from: string };

export type RegisteredNavigation = {
  readonly area: string;
  readonly title: string;
  readonly href: string;
  readonly description: string;
  readonly employees: boolean;
};

export type NavigationManifest = {
  readonly name: string;
  readonly id: string;
  readonly title: string;
  readonly description: string;
  readonly basePath: string;
  readonly featureFlag: string;
  readonly permission: string;
  readonly area: string;
};

export type NavigationArea = { readonly name: string; readonly id: string; readonly title: string };

const importDeclaration = /^import\s+(type\s+)?\{([^}]*)\}\s+from\s+"([^"]+)";[ \t]*\n/gm;
const registryList = /\bexport const navigation\b[^=]*=\s*\[/;
const fixtureList = /\bexport const registeredNavigation\b[^=]*=\s*\[/;
const changeKeysList = /\bexport const changeKeys\b[^=]*=\s*\[/;
const recordTypesList = /\bconst recordTypes\s*=\s*new Map<[^>]*>\(\s*\[/;
const stringPattern = String.raw`"(?:[^"\\]|\\.)*"`;
const manifestDeclaration = /^export const (\w+Navigation) = \{([^}]*)\} as const;/gm;
const areaDeclaration = /^export const (\w+Area) = \{([^}]*)\} as const;/m;

export function registryOf(text: string): readonly RegistryEntry[] {
  const source = text.replaceAll("\r\n", "\n");
  const imported = importsOf(source);
  const list = listAfter(source, registryList, "navigation list");
  return list.elements.map((span) => {
    const name = source.slice(span.start, span.end).replace(/,$/, "").trim();
    return { name, from: imported.get(name) ?? "" };
  });
}

// The import keeps the module specifiers in order, and the entry follows the last entry of the module's domain, so each
// area's entries stay together in the shell; a new domain's area comes last.
export function registerInRegistry(text: string, module: ScaffoldModule): string {
  return preservingShape(text, (source) => {
    const imported = importsOf(source);
    if ([...imported.values()].includes(module.webModule)) throw new Error(`it already imports ${module.webModule}`);
    if (imported.has(module.navigationExport)) throw new Error(`it already imports a navigation named ${module.navigationExport}`);
    const list = listAfter(source, registryList, "navigation list");
    const domainPrefix = `@/features/${module.domain.kebab}/`;
    const last = lastWhere(list, (span) => (imported.get(source.slice(span.start, span.end).replace(/,$/, "").trim()) ?? "").startsWith(domainPrefix));
    const withEntry = insertElement(source, list, last ?? list.elements.at(-1), module.navigationExport);
    const declarations = [...withEntry.matchAll(importDeclaration)];
    const lastDeclaration = declarations.at(-1);
    if (lastDeclaration === undefined) throw new Error("it holds no import to add the module's beside");
    const next = declarations.find((declaration) => ordinal(declaration[3] as string, module.webModule) > 0);
    const line = `import { ${module.navigationExport} } from "${module.webModule}";\n`;
    return insertAt(withEntry, next?.index ?? lastDeclaration.index + lastDeclaration[0].length, line);
  });
}

export function registeredNavigationOf(text: string): readonly RegisteredNavigation[] {
  const source = text.replaceAll("\r\n", "\n");
  const list = listAfter(source, fixtureList, "registeredNavigation list");
  return list.elements.map((span) => fixtureEntry(source.slice(span.start, span.end)));
}

// The list mirrors the registry: the entry follows the last entry of the module's area, or comes last for a new area, laid
// out as the entry before it is. Every generated permission is an administrator's alone, so employees never see the entry.
export function registerInNavigationFixture(text: string, module: ScaffoldModule): string {
  return preservingShape(text, (source) => {
    const list = listAfter(source, fixtureList, "registeredNavigation list");
    const entries = list.elements.map((span) => ({ span, entry: fixtureEntry(source.slice(span.start, span.end)) }));
    if (entries.some(({ entry }) => entry.href === module.basePath)) throw new Error(`it already lists ${module.basePath}`);
    const last = entries.findLast(({ entry }) => entry.area === module.areaTitle)?.span ?? list.elements.at(-1);
    const fields = [
      `area: ${stringLiteral(module.areaTitle)}`,
      `title: ${stringLiteral(module.input.title)}`,
      `href: ${stringLiteral(module.basePath)}`,
      `description: ${stringLiteral(module.input.description)}`,
      "employees: false",
    ];
    const fieldIndentation = last === undefined ? undefined : /\n([ \t]+)\w/.exec(source.slice(last.start, last.end))?.[1];
    if (last === undefined || fieldIndentation === undefined) return insertElement(source, list, last, `{ ${fields.join(", ")} }`);
    const lines = fields.map((field) => `${fieldIndentation}${field},`);
    return insertElement(source, list, last, ["{", ...lines, `${indentationAt(source, last.start)}}`].join("\n"));
  });
}

export function changeKeysOf(text: string): readonly string[] {
  const source = text.replaceAll("\r\n", "\n");
  const list = listAfter(source, changeKeysList, "changeKeys list");
  return list.elements.map((span) => stringValue(source.slice(span.start, span.end).replace(/,$/, "").trim()));
}

export function registerChangeKey(text: string, module: ScaffoldModule): string {
  return preservingShape(text, (source) => {
    const list = listAfter(source, changeKeysList, "changeKeys list");
    const keys = list.elements.map((span) => stringValue(source.slice(span.start, span.end).replace(/,$/, "").trim()));
    if (keys.includes(module.changeKey)) throw new Error(`it already lists ${module.changeKey}`);
    const last = lastWhere(list, (_, index) => (keys[index] as string).startsWith(`${module.domain.kebab}.`));
    return insertElement(source, list, last ?? list.elements.at(-1), stringLiteral(module.changeKey));
  });
}

export function recordTypeKeysOf(text: string): readonly string[] {
  const source = text.replaceAll("\r\n", "\n");
  const list = listAfter(source, recordTypesList, "recordTypes map");
  return list.elements.map((span) => recordTypeKey(source, span));
}

// A record of the module reads as its entity with the default verb: "Ledger note" in the history's filter and "created the
// ledger note" in its sentences, the sentence the generated Playwright spec looks for.
export function registerRecordType(text: string, module: ScaffoldModule): string {
  return preservingShape(text, (source) => {
    const list = listAfter(source, recordTypesList, "recordTypes map");
    const keys = list.elements.map((span) => recordTypeKey(source, span));
    if (keys.includes(module.changeKey)) throw new Error(`it already names ${module.changeKey}`);
    const last = lastWhere(list, (_, index) => (keys[index] as string).startsWith(`${module.domain.kebab}.`));
    const label = module.entityWords.charAt(0).toUpperCase() + module.entityWords.slice(1);
    const entry = `[${stringLiteral(module.changeKey)}, { label: ${stringLiteral(label)}, noun: ${stringLiteral(module.entityWords)} }]`;
    return insertElement(source, list, last ?? list.elements.at(-1), entry);
  });
}

export function navigationManifestsOf(text: string): readonly NavigationManifest[] {
  return [...text.replaceAll("\r\n", "\n").matchAll(manifestDeclaration)].map((match) => {
    const body = match[2] as string;
    return {
      name: match[1] as string,
      id: stringField(body, "id"),
      title: stringField(body, "title"),
      description: stringField(body, "description"),
      basePath: stringField(body, "basePath"),
      featureFlag: stringField(body, "featureFlag"),
      permission: stringField(body, "permission"),
      area: /\barea:\s*(\w+)/.exec(body)?.[1] ?? "",
    };
  });
}

export function areaOf(text: string): NavigationArea | undefined {
  const match = areaDeclaration.exec(text.replaceAll("\r\n", "\n"));
  if (match === null) return undefined;
  return { name: match[1] as string, id: stringField(match[2] as string, "id"), title: stringField(match[2] as string, "title") };
}

export function importsOf(source: string): ReadonlyMap<string, string> {
  const imported = new Map<string, string>();
  for (const match of source.matchAll(importDeclaration)) {
    if (match[1] !== undefined) continue;
    for (const name of (match[2] as string).split(",").map((part) => part.trim().replace(/^type\s+/, ""))) {
      if (name !== "") imported.set(name.split(" as ").at(-1)?.trim() as string, match[3] as string);
    }
  }

  return imported;
}

function fixtureEntry(element: string): RegisteredNavigation {
  const employees = /\bemployees:\s*(true|false)\b/.exec(element)?.[1];
  if (employees === undefined) throw new Error(`its entry ${element.trim()} has no employees: true or false`);
  return {
    area: stringField(element, "area"),
    title: stringField(element, "title"),
    href: stringField(element, "href"),
    description: stringField(element, "description"),
    employees: employees === "true",
  };
}

function stringField(body: string, name: string): string {
  const literal = new RegExp(String.raw`\b${name}:\s*(${stringPattern})`).exec(body)?.[1];
  if (literal === undefined) throw new Error(`its entry ${body.trim().replace(/\s+/g, " ")} has no ${name} written as a string`);
  return stringValue(literal);
}

function recordTypeKey(source: string, span: Span): string {
  const literal = new RegExp(String.raw`^\[\s*(${stringPattern})`).exec(source.slice(span.start, span.end))?.[1];
  if (literal === undefined) throw new Error(`its entry ${source.slice(span.start, span.end).trim()} does not start with its change key`);
  return stringValue(literal);
}

function lastWhere(list: ListLiteral, matches: (span: Span, index: number) => boolean): Span | undefined {
  return list.elements.findLast((span, index) => matches(span, index));
}
