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
  "Only shared/api sends HTTP requests: call the API through callApi or sendApi (shared/api/client.ts), or uploadFile (shared/api/upload.ts).";

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

function restrictedImports(patterns, { apiClient = true } = {}) {
  return ["error", apiClient ? { paths: [apiClientValues], patterns } : { patterns }];
}

function restrictedSyntax(restrictions, { moduleEffectsAllowed = false } = {}) {
  return [
    "error",
    ...typeRoleRestrictions,
    ...objectHrefs,
    ...(moduleEffectsAllowed ? [] : moduleEffects),
    ...restrictions,
  ];
}

const syntaxScopes = {
  source: {
    files: [`src/**/*.${sourceExtensions}`],
    restrictions: apiClientDynamicImports,
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
        "no-restricted-syntax": restrictedSyntax(syntaxScopes.source.restrictions),
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
        "no-restricted-syntax": restrictedSyntax(syntaxScopes["shared-api"].restrictions),
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
        "no-restricted-syntax": restrictedSyntax(syntaxScopes.app.restrictions),
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
        "no-restricted-syntax": restrictedSyntax(syntaxScopes["app-document"].restrictions),
      },
    },
    ...Object.entries(syntaxScopes).map(([scope, { files, restrictions }]) => ({
      name: `erp/feature-boundaries/${scope}-module-effects`,
      files: files.flatMap((scopeFiles) => moduleEffectFiles.map((effectFiles) => [scopeFiles, effectFiles])),
      rules: {
        "no-restricted-syntax": restrictedSyntax(restrictions, { moduleEffectsAllowed: true }),
      },
    })),
  ];
}
