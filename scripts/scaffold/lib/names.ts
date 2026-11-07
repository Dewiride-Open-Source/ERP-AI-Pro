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
const oneSentence = /^[A-Z][^.!?\r\n]*\.$/;
const shortTitle = /^[A-Z][A-Za-z0-9]*(?: [A-Za-z0-9]+)*$/;
const iconName = /^[A-Z][A-Za-z0-9]*Icon$/;
const titleMaxLength = 40;

const reservedRouteDomains = new Set(["auth", "api", "login", "profile", "design", "healthz", "openapi", "scalar"]);
const reservedTypeNames = new Set([
  "Action",
  "Attribute",
  "Delegate",
  "Dewiride",
  "Enum",
  "Error",
  "Errors",
  "Exception",
  "Guid",
  "Microsoft",
  "Module",
  "Object",
  "Result",
  "Results",
  "Shouldly",
  "String",
  "System",
  "Task",
  "Type",
  "TypedResults",
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
  const problems: string[] = [];
  const names = { domain: input.domain, module: input.module, entity: input.entity, entities: input.entities };
  for (const [role, name] of Object.entries(names)) {
    if (!pascalName.test(name)) problems.push(`the ${role} "${name}" is not PascalCase words of letters and digits, each starting with a capital`);
    else if (reservedTypeNames.has(name)) problems.push(`the ${role} "${name}" would shadow a type or namespace the generated code uses`);
  }

  if (pascalName.test(input.module) && formsOf(input.module).words.split(" ").at(-1) === "ai") {
    problems.push(`the module "${input.module}" ends in Ai, the name of an Enterprise companion module`);
  }

  if (pascalName.test(input.domain) && reservedRouteDomains.has(formsOf(input.domain).kebab)) {
    problems.push(`the domain "${input.domain}" would take the route /${formsOf(input.domain).kebab}, which the API or the web app already uses`);
  }

  if (input.entity === input.entities) problems.push(`the entity and its plural are both "${input.entity}"`);

  for (const [role, words] of Object.entries({ singular: input.singular, plural: input.plural })) {
    if (words !== undefined && !proseWords.test(words)) problems.push(`the ${role} "${words}" is not lower-case words separated by single spaces`);
  }

  for (const [role, sentence] of Object.entries({ description: input.description, "read permission": input.readSentence, "create permission": input.createSentence })) {
    if (!oneSentence.test(sentence)) problems.push(`the ${role} "${sentence}" is not one sentence that starts with a capital and ends with a full stop`);
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
