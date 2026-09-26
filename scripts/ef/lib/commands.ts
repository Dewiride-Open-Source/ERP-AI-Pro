import type { DbContextEntry } from "./contexts.ts";

export const startupProject = "Hosts/Api/Dewiride.Erp.Host.Api";
export const migrationsDirectory = "Persistence/Migrations";

export type BuildOptions = {
  noBuild?: boolean;
  configuration?: string;
};

export type UpdateOptions = BuildOptions & {
  migration?: string;
  connection?: string;
};

export type ScriptOptions = BuildOptions & {
  from?: string;
  to?: string;
  idempotent?: boolean;
  output?: string;
};

export function addArguments(context: DbContextEntry, name: string): string[] {
  return ["ef", "migrations", "add", name, ...target(context), "--output-dir", migrationsDirectory];
}

export function updateArguments(context: DbContextEntry, options: UpdateOptions = {}): string[] {
  return [
    "ef",
    "database",
    "update",
    ...(options.migration ? [options.migration] : []),
    ...target(context),
    ...(options.connection ? ["--connection", options.connection] : []),
    ...build(options),
  ];
}

export function pendingArguments(context: DbContextEntry, options: BuildOptions = {}): string[] {
  return ["ef", "migrations", "has-pending-model-changes", ...target(context), ...build(options)];
}

export function scriptArguments(context: DbContextEntry, options: ScriptOptions = {}): string[] {
  return [
    "ef",
    "migrations",
    "script",
    ...(options.from ? [options.from] : options.to ? ["0"] : []),
    ...(options.to ? [options.to] : []),
    ...target(context),
    ...(options.idempotent ? ["--idempotent"] : []),
    ...(options.output ? ["--output", options.output] : []),
    ...build(options),
  ];
}

function target(context: DbContextEntry): string[] {
  return ["--context", context.contextName, "--project", context.project, "--startup-project", startupProject];
}

function build(options: BuildOptions): string[] {
  return [...(options.noBuild ? ["--no-build"] : []), ...(options.configuration ? ["--configuration", options.configuration] : [])];
}
