import { formsOf, type NameForms, type ScaffoldInput, tokensOf } from "../names.ts";

export type ScaffoldModule = {
  readonly input: ScaffoldInput;
  readonly tokens: ReadonlyMap<string, string>;
  readonly domain: NameForms;
  readonly module: NameForms;
  readonly entity: NameForms;
  readonly entities: NameForms;
  readonly entityWords: string;
  readonly newDomain: boolean;
  readonly areaTitle: string;
  readonly assembly: string;
  readonly moduleClass: string;
  readonly featureFlag: string;
  readonly schema: string;
  readonly routePrefix: string;
  readonly contextKey: string;
  readonly changeKey: string;
  readonly readPermission: string;
  readonly createPermission: string;
  readonly folder: string;
  readonly projects: readonly [string, string, string, string];
  readonly moduleProject: string;
  readonly webModule: string;
  readonly navigationExport: string;
  readonly navigationId: string;
  readonly basePath: string;
  readonly apiModels: readonly string[];
};

// The four projects in the order the solution lists them: the module's folder holds Contracts and Module, its Tests folder
// the unit tests before the integration tests.
export function moduleProjects(domain: string, module: string): readonly [string, string, string, string] {
  const assembly = `Dewiride.Erp.Modules.${domain}.${module}`;
  const folder = `Modules/${domain}/${module}`;
  return [
    `${folder}/Contracts/${assembly}.Contracts/${assembly}.Contracts.csproj`,
    `${folder}/Module/${assembly}/${assembly}.csproj`,
    `${folder}/Tests/UnitTests/${assembly}.UnitTests/${assembly}.UnitTests.csproj`,
    `${folder}/Tests/IntegrationTests/${assembly}.IntegrationTests/${assembly}.IntegrationTests.csproj`,
  ];
}

export function describeModule(input: ScaffoldInput, options: { readonly newDomain: boolean; readonly areaTitle: string }): ScaffoldModule {
  const domain = formsOf(input.domain);
  const module = formsOf(input.module);
  const entity = formsOf(input.entity);
  const entities = formsOf(input.entities);
  const changeKey = `${domain.kebab}.${module.kebab}.${entities.kebab}`;
  const projects = moduleProjects(domain.pascal, module.pascal);
  return {
    input,
    tokens: tokensOf(input),
    domain,
    module,
    entity,
    entities,
    entityWords: input.singular ?? entity.words,
    newDomain: options.newDomain,
    areaTitle: options.areaTitle,
    assembly: `Dewiride.Erp.Modules.${domain.pascal}.${module.pascal}`,
    moduleClass: `ErpModules.${domain.pascal}.${module.pascal}.${module.pascal}Module`,
    featureFlag: `Erp.Modules.${domain.pascal}.${module.pascal}`,
    schema: `${domain.snake}_${module.snake}`,
    routePrefix: `/${domain.kebab}/${module.kebab}`,
    contextKey: `${domain.kebab}-${module.kebab}`,
    changeKey,
    readPermission: `${changeKey}.read`,
    createPermission: `${changeKey}.create`,
    folder: `Modules/${domain.pascal}/${module.pascal}`,
    projects,
    moduleProject: projects[1],
    webModule: `@/features/${domain.kebab}/${module.kebab}`,
    navigationExport: `${entities.camel}Navigation`,
    navigationId: `${domain.kebab}-${module.kebab}-${entities.kebab}`,
    basePath: `/${domain.kebab}/${module.kebab}/${entities.kebab}`,
    apiModels: [`${entity.pascal}Response`, `Create${entity.pascal}Request`, `PagedResponseOf${entity.pascal}Response`],
  };
}
