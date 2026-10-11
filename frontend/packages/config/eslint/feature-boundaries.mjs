import { typeRoleRestrictions } from "./type-roles.mjs";

const sourceExtensions = "{js,jsx,mjs,cjs,ts,tsx,mts,cts}";
const deepestModuleFolder = 8;
const pathSegment = "[a-z0-9][a-z0-9._-]*";
const kebabSegment = "[a-z][a-z0-9-]*";

const moduleInternals = {
  regex: "^@/features/[^/]+/(?!_shared(?:/|$))[^/]+/(?!index$).",
  message:
    "Import another module only through its public surface, @/features/<domain>/<module>; inside a module, use a relative path.",
};

const domainSharedAlias = {
  regex: "^@/features/[^/]+/_shared(?:/|$)",
  message:
    "Import a domain's _shared code with a relative path from a module of the same domain; other domains cannot use it.",
};

const appAlias = {
  regex: "^@/app(?:/|$)",
  message: "Nothing imports from app/: route files only compose features and shared code.",
};

const detour = {
  regex: String.raw`(?:^|/)(?!\.\.?(?:/|$))[^/]+/\.\.(?:/|$)`,
  message: "Write the import path without a '..' detour; the boundary rules check the normalised path.",
};

const currentFolderDetour = {
  regex: String.raw`.+/\.(?:/|$)`,
  message:
    "Write the import path without a './' after its start; the boundary rules check the normalised path.",
};

const featuresFromShared = [
  { regex: "^@/features(?:/|$)" },
  { regex: String.raw`^(?:\./)?(?:\.\./)+(?:[^/]+/)*features(?:/|$)` },
].map((pattern) => ({
  ...pattern,
  message: "shared/ never imports features/: let the route in app/ pass what it needs in as props.",
}));

const relativeFromApp = {
  regex: String.raw`^\.\.?(?:/|$)`,
  message: "Route files import with the @/ alias, never a relative path.",
};

function moduleEscape(depth) {
  return {
    regex: String.raw`^(?:\./)?(?:\.\.(?:/|$)){${depth + 1}}(?!_shared(?:/|$))`,
    message:
      "A relative import stays inside its module or reaches its domain's _shared folder; import another module through @/features/<domain>/<module>.",
  };
}

const selectorRegexSlash = String.raw`\u` + "002F";

function routeSpecifiers(extraSpecifiers) {
  return [
    `next(?:/${pathSegment})*`,
    "react",
    `@/shared(?:/${pathSegment})+`,
    "@/features/registry",
    `@/features/${kebabSegment}/${kebabSegment}(?:/index)?`,
    ...extraSpecifiers,
  ];
}

function routeAllowList(specifiers, message) {
  return { regex: String.raw`^(?!\.)(?!(?:${specifiers.join("|")})$)`, caseSensitive: true, message };
}

function dynamicRouteAllowList(specifiers, message) {
  const allowed = specifiers.join("|").replaceAll("/", selectorRegexSlash);
  return [
    { selector: 'ImportExpression[source.type!="Literal"]', message },
    { selector: `ImportExpression[source.type="Literal"][source.value!=/^(?:${allowed})$/]`, message },
  ];
}

const routeImportSpecifiers = routeSpecifiers([]);
const routeImportsMessage =
  "Route files import only next, react, @/shared/*, @/features/registry and a module's public surface @/features/<domain>/<module>; move anything else into the feature or shared/.";

const documentImportSpecifiers = routeSpecifiers([
  `@dewiride/erp-ui(?:/${pathSegment})+`,
  `geist(?:/${pathSegment})+`,
]);
const documentImportsMessage =
  "The root layout and the global error import only next, react, @dewiride/erp-ui/*, geist/*, @/shared/*, @/features/registry and a module's public surface @/features/<domain>/<module>.";

const apiClientMessage =
  "Only shared/api calls the API: import @dewiride/erp-api-client types with `import type` and send requests through shared/api/client.ts.";

const apiClientValues = {
  name: "@dewiride/erp-api-client",
  allowTypeImports: true,
  message: apiClientMessage,
};

const apiClientDynamicImports = [
  `ImportExpression[source.value="${apiClientValues.name}"]`,
  `ImportExpression[source.type="TemplateLiteral"] TemplateElement[value.cooked="${apiClientValues.name}"]`,
].map((selector) => ({ selector, message: apiClientMessage }));

const httpMessage =
  "Only shared/api sends HTTP requests: call the API through callApi or sendApi (shared/api/client.ts), uploadFile (shared/api/upload.ts), or readSessionTimes or renewSessionTimes (shared/api/session/session-watch.ts).";

const httpGlobals = ["fetch", "XMLHttpRequest", "EventSource"];

const httpGlobalRestrictions = httpGlobals.map((name) => ({ name, message: httpMessage }));

const httpPropertyRestrictions = ["globalThis", "window", "self"].flatMap((object) =>
  httpGlobals.map((property) => ({ object, property, message: httpMessage })),
);

const hrefWrapper =
  ":matches(TSAsExpression, TSSatisfiesExpression, TSNonNullExpression, ConditionalExpression, LogicalExpression)";
const deepestHrefWrapping = 3;

const objectHrefs = Array.from({ length: deepestHrefWrapping + 1 }, (_, wrapping) =>
  [
    'JSXAttribute[name.name="href"] > JSXExpressionContainer',
    ...Array.from({ length: wrapping }, () => hrefWrapper),
    "ObjectExpression",
  ].join(" > "),
).map((selector) => ({
  selector,
  message: "Write href as a route string so typed routes check it; an object href is not checked.",
}));

const routeMarkup = {
  selector: 'JSXOpeningElement[name.type="JSXIdentifier"][name.name=/^[a-z]/]',
  message:
    "A route file only composes: render a component exported by a module's index.ts or by shared/, not an HTML element.",
};

const documentMarkup = {
  selector: 'JSXOpeningElement[name.type="JSXIdentifier"][name.name=/^(?!(?:html|body)$)[a-z]/]',
  message:
    "The root layout and the global error render only <html> and <body>; put anything else in a shared/layout component.",
};

export const sideEffectFiles = ["src/instrumentation-client.ts"];

const moduleEffectFiles = [...sideEffectFiles, `src/**/*.test.${sourceExtensions}`];

const executableStatements = [
  "ExpressionStatement:not([directive])",
  "IfStatement",
  "SwitchStatement",
  "TryStatement",
  "BlockStatement",
  "LabeledStatement",
  "ThrowStatement",
  "ForStatement",
  "ForInStatement",
  "ForOfStatement",
  "WhileStatement",
  "DoWhileStatement",
];

const moduleEffects = [
  {
    selector: `Program > :matches(${executableStatements.join(", ")})`,
    message:
      "A web source file only defines exports, because apps/web/package.json declares it free of side effects: move this statement into a function, or list the file both under sideEffects there and in sideEffectFiles of packages/config/eslint/feature-boundaries.mjs (ADR-0025, rule X3).",
  },
  {
    selector: [
      "ImportDeclaration[specifiers.length=0]",
      ':not([importKind="type"])',
      String.raw`[source.value=/^(?:\.|@` + selectorRegexSlash + ")/]",
      String.raw`[source.value!=/\.css$/]`,
    ].join(""),
    message:
      "Import a name this file exports instead of the whole file: apps/web/package.json declares web source files free of side effects, so a bare import of one may be dropped (ADR-0025).",
  },
];

const serverFunctionMessage =
  "A Server Function starts with `const person = await requireSignedInPerson();` followed by `if (!holdsPermission(person, <permission>)) return <refusal>;`: it changes something only for a person the API has just confirmed who holds the permission (ADR-0036, ADR-0041, rule X4).";

const harnessServerFunctionMessage =
  "A design-harness Server Function starts with `await requireSignedInPerson()`: it changes only demo data, so a person the API has just confirmed may use it (ADR-0036, rule X4).";

const serverFunctionExport = 'Program > ExpressionStatement[directive="use server"] ~ ';

const serverFunctionBody = `${serverFunctionExport}ExportNamedDeclaration > FunctionDeclaration > BlockStatement`;

const serverFunctionFileShapes = [
  `${serverFunctionExport}ExportNamedDeclaration:not([exportKind="type"]):not([declaration.type="FunctionDeclaration"])`,
  `${serverFunctionExport}ExportDefaultDeclaration`,
  `${serverFunctionExport}ExportAllDeclaration:not([exportKind="type"])`,
];

function awaitsSignedInPerson(path) {
  return [
    `[${path}.type="AwaitExpression"]`,
    `[${path}.argument.type="CallExpression"]`,
    `[${path}.argument.callee.type="Identifier"]`,
    `[${path}.argument.callee.name="requireSignedInPerson"]`,
    `[${path}.argument.arguments.length=0]`,
  ].join("");
}

const signedInPerson = "person";

const personCheck = [
  'VariableDeclaration[kind="const"][declarations.length=1]',
  `[declarations.0.id.type="Identifier"][declarations.0.id.name="${signedInPerson}"]`,
  awaitsSignedInPerson("declarations.0.init"),
].join("");

const permissionCheck = [
  "IfStatement:not([alternate])",
  '[test.type="UnaryExpression"][test.operator="!"]',
  '[test.argument.type="CallExpression"][test.argument.callee.type="Identifier"]',
  '[test.argument.callee.name="holdsPermission"][test.argument.arguments.length=2]',
  `[test.argument.arguments.0.type="Identifier"][test.argument.arguments.0.name="${signedInPerson}"]`,
  '[test.argument.arguments.1.type!="SpreadElement"]',
  '[consequent.type="ReturnStatement"][consequent.argument]',
].join("");

const serverFunctions = [
  `${serverFunctionBody} > :first-child:not(${personCheck})`,
  `${serverFunctionBody}[body.length=0]`,
  `${serverFunctionBody}[body.length=1] > ${personCheck}`,
  `${serverFunctionBody} > ${personCheck}:first-child + :not(${permissionCheck})`,
  ...serverFunctionFileShapes,
].map((selector) => ({ selector, message: serverFunctionMessage }));

const harnessSessionCheck = `:matches(ExpressionStatement${awaitsSignedInPerson("expression")}, VariableDeclaration[declarations.length=1]${awaitsSignedInPerson("declarations.0.init")})`;

const harnessServerFunctions = [
  `${serverFunctionBody} > :first-child:not(${harnessSessionCheck})`,
  `${serverFunctionBody}[body.length=0]`,
  ...serverFunctionFileShapes,
].map((selector) => ({ selector, message: harnessServerFunctionMessage }));

function restrictedImports(patterns, { apiClient = true } = {}) {
  return ["error", apiClient ? { paths: [apiClientValues], patterns } : { patterns }];
}

function restrictedSyntax(
  { restrictions, serverFunctionRules = serverFunctions },
  { moduleEffectsAllowed = false } = {},
) {
  return [
    "error",
    ...typeRoleRestrictions,
    ...objectHrefs,
    ...serverFunctionRules,
    ...(moduleEffectsAllowed ? [] : moduleEffects),
    ...restrictions,
  ];
}

const syntaxScopes = {
  source: {
    files: [`src/**/*.${sourceExtensions}`],
    restrictions: apiClientDynamicImports,
  },
  "design-harness": {
    files: [`src/features/platform/design/**/*.${sourceExtensions}`],
    restrictions: apiClientDynamicImports,
    serverFunctionRules: harnessServerFunctions,
  },
  "shared-api": {
    files: [`src/shared/api/**/*.${sourceExtensions}`],
    restrictions: [],
  },
  app: {
    files: [`src/app/**/*.${sourceExtensions}`],
    restrictions: [routeMarkup, ...dynamicRouteAllowList(routeImportSpecifiers, routeImportsMessage)],
  },
  "app-document": {
    files: [`src/app/layout.${sourceExtensions}`, `src/app/global-error.${sourceExtensions}`],
    restrictions: [
      documentMarkup,
      ...dynamicRouteAllowList(documentImportSpecifiers, documentImportsMessage),
    ],
  },
};

const sourceImports = [moduleInternals, domainSharedAlias, appAlias, detour, currentFolderDetour];
const sharedImports = [appAlias, detour, currentFolderDetour, ...featuresFromShared];

export function featureBoundaryConfigs() {
  return [
    {
      name: "erp/feature-boundaries/source",
      files: syntaxScopes.source.files,
      rules: {
        "no-restricted-imports": restrictedImports(sourceImports),
        "no-restricted-syntax": restrictedSyntax(syntaxScopes.source),
        "no-restricted-globals": ["error", ...httpGlobalRestrictions],
        "no-restricted-properties": ["error", ...httpPropertyRestrictions],
      },
    },
    ...Array.from({ length: deepestModuleFolder + 1 }, (_, depth) => ({
      name: `erp/feature-boundaries/module-depth-${depth}`,
      files: [`src/features/*/*/${"*/".repeat(depth)}*.${sourceExtensions}`],
      rules: {
        "no-restricted-imports": restrictedImports([...sourceImports, moduleEscape(depth)]),
      },
    })),
    {
      name: "erp/feature-boundaries/design-harness",
      files: syntaxScopes["design-harness"].files,
      rules: {
        "no-restricted-syntax": restrictedSyntax(syntaxScopes["design-harness"]),
      },
    },
    {
      name: "erp/feature-boundaries/shared",
      files: [`src/shared/**/*.${sourceExtensions}`],
      rules: {
        "no-restricted-imports": restrictedImports(sharedImports),
      },
    },
    {
      name: "erp/feature-boundaries/shared-api",
      files: syntaxScopes["shared-api"].files,
      rules: {
        "no-restricted-imports": restrictedImports(sharedImports, { apiClient: false }),
        "no-restricted-syntax": restrictedSyntax(syntaxScopes["shared-api"]),
        "no-restricted-globals": "off",
        "no-restricted-properties": "off",
      },
    },
    {
      name: "erp/feature-boundaries/app",
      files: syntaxScopes.app.files,
      rules: {
        "no-restricted-imports": restrictedImports(
          [relativeFromApp, routeAllowList(routeImportSpecifiers, routeImportsMessage)],
          { apiClient: false },
        ),
        "no-restricted-syntax": restrictedSyntax(syntaxScopes.app),
      },
    },
    {
      name: "erp/feature-boundaries/app-document",
      files: syntaxScopes["app-document"].files,
      rules: {
        "no-restricted-imports": restrictedImports(
          [relativeFromApp, routeAllowList(documentImportSpecifiers, documentImportsMessage)],
          { apiClient: false },
        ),
        "no-restricted-syntax": restrictedSyntax(syntaxScopes["app-document"]),
      },
    },
    ...Object.entries(syntaxScopes).map(([scope, syntax]) => ({
      name: `erp/feature-boundaries/${scope}-module-effects`,
      files: syntax.files.flatMap((scopeFiles) =>
        moduleEffectFiles.map((effectFiles) => [scopeFiles, effectFiles]),
      ),
      rules: {
        "no-restricted-syntax": restrictedSyntax(syntax, { moduleEffectsAllowed: true }),
      },
    })),
  ];
}
