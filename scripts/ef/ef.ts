import { spawnSync } from "node:child_process";
import { parseArgs } from "node:util";

import { addArguments, pendingArguments, scriptArguments, updateArguments } from "./lib/commands.ts";
import { backendRoot, type DbContextEntry, discoverContexts } from "./lib/contexts.ts";

const usage = `Usage: node scripts/ef/ef.ts <command> [options]

Commands:
  list                                   list every discovered DbContext and its key
  add <Name> --context <key>             add a migration to Persistence/Migrations of the context's project
  update --context <key> | --all         apply migrations (optionally up to [--migration <Name>])
  pending --context <key> | --all        fail when the model has changes no migration captures
  script --context <key>                 write the SQL of [--from <A>] [--to <B>] [--idempotent] [--output <file>]

Options: --no-build, --configuration <Debug|Release>, --connection <string> (update only; prefer user secrets).`;

const { positionals, values } = parseArgs({
  allowPositionals: true,
  options: {
    context: { type: "string" },
    all: { type: "boolean", default: false },
    "no-build": { type: "boolean", default: false },
    configuration: { type: "string" },
    connection: { type: "string" },
    migration: { type: "string" },
    from: { type: "string" },
    to: { type: "string" },
    idempotent: { type: "boolean", default: false },
    output: { type: "string" },
    help: { type: "boolean", default: false },
  },
});

const [command, name] = positionals;
if (values.help || !command) {
  console.log(usage);
  process.exit(values.help ? 0 : 64);
}

const contexts = discoverContexts();
const build = { noBuild: values["no-build"], configuration: values.configuration };

switch (command) {
  case "list":
    for (const context of contexts) console.log(`${context.key.padEnd(24)} ${context.contextName.padEnd(28)} ${context.project}`);
    break;
  case "add":
    if (!name) fail("add needs a migration name, for example: node scripts/ef/ef.ts add AddInvoices --context sales");
    run(addArguments(single(), name));
    break;
  case "update":
    for (const context of selected()) run(updateArguments(context, { ...build, migration: values.migration, connection: values.connection }));
    break;
  case "pending": {
    const failed = selected().filter((context) => !succeeds(pendingArguments(context, build)));
    if (failed.length > 0) fail(`pending model changes or errors in: ${failed.map((context) => context.key).join(", ")}. Add a migration with the add command.`);
    break;
  }
  case "script":
    run(scriptArguments(single(), { ...build, from: values.from, to: values.to, idempotent: values.idempotent, output: values.output }));
    break;
  default:
    fail(`unknown command "${command}".\n\n${usage}`);
}

function single(): DbContextEntry {
  if (values.all) fail(`${command} works on one context; pass --context <key>.`);
  return find(values.context);
}

function selected(): DbContextEntry[] {
  if (values.all && values.context) fail("pass either --context <key> or --all, not both.");
  return values.all ? contexts : [find(values.context)];
}

function find(key: string | undefined): DbContextEntry {
  if (!key) fail(`pass --context <key>; the keys are: ${contexts.map((context) => context.key).join(", ")}.`);
  const context = contexts.find((candidate) => candidate.key === key);
  if (!context) fail(`no DbContext has the key "${key}"; the keys are: ${contexts.map((candidate) => candidate.key).join(", ")}.`);
  return context;
}

function run(args: string[]): void {
  if (!succeeds(args)) fail(`dotnet ${args.slice(0, 3).join(" ")} failed.`);
}

function succeeds(args: string[]): boolean {
  console.log(`▶ dotnet ${args.join(" ")}`);
  return spawnSync("dotnet", args, { cwd: backendRoot, stdio: "inherit" }).status === 0;
}

function fail(message: string): never {
  console.error(`ef: ${message}`);
  process.exit(1);
}
