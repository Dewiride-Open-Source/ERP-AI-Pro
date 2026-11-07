export type NameForms = {
  readonly pascal: string;
  readonly camel: string;
  readonly kebab: string;
  readonly snake: string;
  readonly words: string;
};

export type ScaffoldInput = {
  readonly domain: string;
  readonly module: string;
  readonly entity: string;
  readonly entities: string;
  readonly singular?: string;
  readonly plural?: string;
  readonly title: string;
  readonly description: string;
  readonly readSentence: string;
  readonly createSentence: string;
  readonly areaTitle?: string;
  readonly icon: string;
};

const pascalName = /^(?:[A-Z][a-z0-9]+)+$/;
const wordBoundary = /(?<=[a-z0-9])(?=[A-Z])/;
const proseWords = /^[a-z][a-z0-9]*(?: [a-z][a-z0-9]*)*$/;
const oneSentence = /^[A-Z][A-Za-z0-9 ,;:'()/-]*\.$/;
const shortTitle = /^[A-Z][A-Za-z0-9]*(?: [A-Za-z0-9]+)*$/;
const iconName = /^[A-Z][A-Za-z0-9]*Icon$/;
const titleMaxLength = 40;
const changeKeyMaxLength = 64;

const reservedRouteDomains = new Set(["auth", "api", "login", "profile", "design", "healthz", "openapi", "scalar"]);
const reservedClientNames = new Set([
  "any",
  "as",
  "async",
  "await",
  "boolean",
  "break",
  "case",
  "catch",
  "class",
  "const",
  "constructor",
  "continue",
  "debugger",
  "declare",
  "default",
  "delete",
  "do",
  "else",
  "enum",
  "export",
  "extends",
  "false",
  "finally",
  "for",
  "from",
  "function",
  "get",
  "if",
  "implements",
  "import",
  "in",
  "instanceof",
  "interface",
  "is",
  "keyof",
  "let",
  "module",
  "namespace",
  "never",
  "new",
  "null",
  "number",
  "object",
  "of",
  "package",
  "private",
  "protected",
  "public",
  "readonly",
  "require",
  "return",
  "set",
  "static",
  "string",
  "super",
  "switch",
  "symbol",
  "this",
  "throw",
  "true",
  "try",
  "type",
  "typeof",
  "undefined",
  "unique",
  "unknown",
  "var",
  "void",
  "while",
  "with",
  "yield",
]);
const reservedNames = new Set([
  "Action",
  "Application",
  "Attribute",
  "BuildingBlocks",
  "Commands",
  "Contracts",
  "Delegate",
  "Dewiride",
  "Domain",
  "Endpoints",
  "Enum",
  "Erp",
  "Error",
  "Errors",
  "Exception",
  "Guid",
  "Hosts",
  "Integration",
  "IntegrationTests",
  "Kernel",
  "Microsoft",
  "Module",
  "Modules",
  "Object",
  "Persistence",
  "Queries",
  "Requests",
  "Responses",
  "Result",
  "Results",
  "Shouldly",
  "String",
  "System",
  "Task",
  "Testing",
  "Tests",
  "Type",
  "TypedResults",
  "UnitTests",
  "Xunit",
]);

export function formsOf(pascal: string): NameForms {
  const words = pascal.split(wordBoundary).map((word) => word.toLowerCase());
  return {
    pascal,
    camel: pascal.charAt(0).toLowerCase() + pascal.slice(1),
    kebab: words.join("-"),
    snake: words.join("_"),
    words: words.join(" "),
  };
}

export function nameProblems(input: ScaffoldInput): string[] {
  const names = { domain: input.domain, module: input.module, entity: input.entity, entities: input.entities };
  const malformed = Object.entries(names).flatMap(([role, name]) =>
    pascalName.test(name) ? [] : [`the ${role} "${name}" is not PascalCase words of letters and digits, each starting with a capital`],
  );

  return [...malformed, ...namingClashes(input), ...textProblems(input)];
}

function namingClashes(input: ScaffoldInput): string[] {
  const problems: string[] = [];
  const names = { domain: input.domain, module: input.module, entity: input.entity, entities: input.entities };
  for (const [role, name] of Object.entries(names).filter(([, name]) => pascalName.test(name))) {
    if (reservedNames.has(name)) problems.push(`the ${role} "${name}" would shadow a type or namespace the generated code uses`);
    if (role !== "entity" && reservedClientNames.has(formsOf(name).camel)) {
      problems.push(`the ${role} "${name}" becomes the API client property "${formsOf(name).camel}", a word the client generator renames`);
    }
  }

  for (const [role, name] of Object.entries({ module: input.module, domain: input.domain })) {
    if (input.entity === name) problems.push(`the entity "${input.entity}" is also the ${role}, whose namespace would hide the entity's type`);
  }

  if (input.entity === input.entities) problems.push(`the entity and its plural are both "${input.entity}"`);

  if (pascalName.test(input.module) && formsOf(input.module).words.split(" ").at(-1) === "ai") {
    problems.push(`the module "${input.module}" ends in Ai, the name of an Enterprise companion module`);
  }

  const route = formsOf(input.domain).kebab;
  if (pascalName.test(input.domain) && reservedRouteDomains.has(route)) {
    problems.push(`the domain "${input.domain}" would take the route /${route}, which the API or the web app already uses`);
  }

  const keyNames = [input.domain, input.module, input.entities];
  const changeKey = keyNames.map((name) => formsOf(name).kebab).join(".");
  if (keyNames.every((name) => pascalName.test(name)) && changeKey.length > changeKeyMaxLength) {
    problems.push(`the change key "${changeKey}" is longer than the ${changeKeyMaxLength} characters a change record keeps`);
  }

  return problems;
}

function textProblems(input: ScaffoldInput): string[] {
  const problems: string[] = [];
  for (const [role, words] of Object.entries({ singular: input.singular, plural: input.plural })) {
    if (words !== undefined && !proseWords.test(words)) problems.push(`the ${role} "${words}" is not lower-case words separated by single spaces`);
  }

  for (const [role, sentence] of Object.entries({ description: input.description, "read permission": input.readSentence, "create permission": input.createSentence })) {
    if (!oneSentence.test(sentence)) {
      problems.push(`the ${role} "${sentence}" is not one sentence of letters, digits, spaces and , ; : ' ( ) / - that starts with a capital and ends with a full stop`);
    }
  }

  for (const [role, title] of Object.entries({ title: input.title, "area title": input.areaTitle })) {
    if (title !== undefined && (!shortTitle.test(title) || title.length > titleMaxLength)) {
      problems.push(`the ${role} "${title}" is not a title of at most ${titleMaxLength} letters, digits and single spaces starting with a capital`);
    }
  }

  if (!iconName.test(input.icon)) problems.push(`the icon "${input.icon}" is not the name of a lucide-react icon export, such as FileTextIcon`);

  return problems;
}

export function tokensOf(input: ScaffoldInput): ReadonlyMap<string, string> {
  const tokens = new Map<string, string>();
  const stems = [
    ["Domain", formsOf(input.domain), undefined],
    ["Module", formsOf(input.module), undefined],
    ["Entity", formsOf(input.entity), input.singular],
    ["Entities", formsOf(input.entities), input.plural],
  ] as const;
  for (const [stem, forms, words] of stems) {
    const lower = stem.charAt(0).toLowerCase() + stem.slice(1);
    const prose = words ?? forms.words;
    tokens.set(`__${stem}__`, forms.pascal);
    tokens.set(`__${lower}__`, forms.camel);
    tokens.set(`__${lower}-kebab__`, forms.kebab);
    tokens.set(`__${lower}_snake__`, forms.snake);
    tokens.set(`__${lower}-words__`, prose);
    tokens.set(`__${stem}-words__`, prose.charAt(0).toUpperCase() + prose.slice(1));
  }

  tokens.set("__Title__", input.title);
  tokens.set("__Description__", input.description);
  tokens.set("__ReadSentence__", input.readSentence);
  tokens.set("__CreateSentence__", input.createSentence);
  tokens.set("__Icon__", input.icon);
  if (input.areaTitle !== undefined) tokens.set("__AreaTitle__", input.areaTitle);

  return tokens;
}
