import { dirname, resolve } from "node:path";
import { parseArgs } from "node:util";
import { fileURLToPath } from "node:url";

import { commandLines, completionSteps, quoted, runCompletion, spawnStep } from "./lib/completion.ts";
import type { ScaffoldInput } from "./lib/names.ts";
import { completionOutputs, planModule } from "./lib/scaffold.ts";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
const defaultIcon = "FileTextIcon";

const usage = `Usage: node scripts/scaffold/new-module.ts <Domain>/<Module> --entity <Entity> --title "<navigation title>"
         --description "<sentence>" --read-permission "<sentence>" --create-permission "<sentence>"
         [--entities <Entities>] [--singular "<words>"] [--plural "<words>"] [--area-title "<title>"]
         [--icon <LucideIcon>] [--files-only]

Creates a Community module with one feature that lists, opens and adds records of <Entity>: its four projects, the web
pages behind the module's flag and their Playwright specs, and registers it everywhere docs/architecture/adding-a-module.md
lists. Every sentence is shown to people and approved by the owner first.

  --entity             the record, a PascalCase singular (LedgerNote)
  --entities           its plural (default: <Entity> + s, es or ies)
  --title              the navigation entry's title (Ledger notes)
  --description        the navigation entry's description, one sentence
  --read-permission    what a holder of <domain>.<module>.<entities>.read may do, one sentence
  --create-permission  what a holder of <domain>.<module>.<entities>.create may do, one sentence
  --singular, --plural how people read the record in prose (default: the words of the names)
  --area-title         the navigation area's title, for a new domain only
  --icon               the lucide-react icon export of the navigation entry (default: ${defaultIcon})
  --files-only         write and register the files, then print the commands that finish the module`;

const parsed = parseCommandLine();
if (parsed === "help") {
  console.log(usage);
  process.exit(0);
}

if (Array.isArray(parsed)) refuse(parsed, true);
const { input, filesOnly } = parsed as { input: ScaffoldInput; filesOnly: boolean };

const plan = planModule(repoRoot, input, { filesOnly, environment: process.env });
if (plan.problems.length > 0 || plan.module === undefined) refuse(plan.problems, false);
const module = plan.module;
const changes = plan.changes.changes;

try {
  await plan.changes.apply();
} catch (error) {
  console.error(`✖ the files could not be written, and every file this run touched is back as it was: ${(error as Error).message}`);
  process.exit(1);
}

const created = changes.filter(({ kind }) => kind === "create");
const edited = changes.filter(({ kind }) => kind === "edit");
console.log(`✔ ${module.domain.pascal}/${module.module.pascal}: created ${created.length} files and registered the module in ${edited.length} more`);
for (const { kind, path } of [...created, ...edited]) console.log(`  ${kind === "create" ? "created" : "edited "} ${path}`);

const steps = completionSteps(module, changes.map(({ path }) => path));
let remaining = steps;
if (!filesOnly) {
  const outcome = runCompletion(steps, spawnStep(repoRoot));
  remaining = [...outcome.remaining];
  if (outcome.failed !== undefined) console.error(`\n✖ the step "${outcome.failed.name}" failed; fix its cause, then finish from the repository root with:`);
} else {
  console.log("\nFinish the module from the repository root with:");
}

if (remaining.length > 0) for (const line of commandLines(remaining)) console.log(`  ${line}`);
else console.log(`\n✔ ${module.domain.pascal}/${module.module.pascal} is written, registered, migrated and in the OpenAPI contract and the web client`);

console.log("\nTo undo this run entirely:");
console.log(`  git restore -- ${[...edited.map(({ path }) => path), ...completionOutputs].map(quoted).join(" ")}`);
console.log(`  git clean -fd -- ${[...plan.createdRoots, completionOutputs[1]].map(quoted).join(" ")}`);
console.log(`\nBefore it ships, seed ${module.featureFlag} into App Configuration, with the owner's consent:`);
console.log("  bash scripts/azure/seed.sh --dry-run --label all");
console.log("  bash scripts/azure/seed.sh --label all");
process.exit(remaining.length > 0 && !filesOnly ? 1 : 0);

function parseCommandLine(): { input: ScaffoldInput; filesOnly: boolean } | string[] | "help" {
  let values: Record<string, string | boolean | undefined>;
  let positionals: string[];
  try {
    ({ values, positionals } = parseArgs({
      allowPositionals: true,
      options: {
        entity: { type: "string" },
        entities: { type: "string" },
        title: { type: "string" },
        description: { type: "string" },
        "read-permission": { type: "string" },
        "create-permission": { type: "string" },
        singular: { type: "string" },
        plural: { type: "string" },
        "area-title": { type: "string" },
        icon: { type: "string" },
        "files-only": { type: "boolean", default: false },
        help: { type: "boolean", default: false },
      },
    }));
  } catch (error) {
    return [(error as Error).message];
  }

  if (values.help === true) return "help";
  const problems: string[] = [];
  const target = positionals.length === 1 ? /^([^/\\]+)[/\\]([^/\\]+)$/.exec(positionals[0] as string) : null;
  if (target === null) problems.push("name the module once, as <Domain>/<Module>");
  const required = ["entity", "title", "description", "read-permission", "create-permission"].filter((name) => typeof values[name] !== "string");
  for (const name of required) problems.push(`--${name} is required`);
  if (problems.length > 0 || target === null) return problems;

  const text = (name: string): string | undefined => values[name] as string | undefined;
  const entity = text("entity") as string;
  return {
    filesOnly: values["files-only"] === true,
    input: {
      domain: target[1] as string,
      module: target[2] as string,
      entity,
      entities: text("entities") ?? pluralOf(entity),
      singular: text("singular"),
      plural: text("plural"),
      title: text("title") as string,
      description: text("description") as string,
      readSentence: text("read-permission") as string,
      createSentence: text("create-permission") as string,
      areaTitle: text("area-title"),
      icon: text("icon") ?? defaultIcon,
    },
  };
}

function pluralOf(entity: string): string {
  if (/[^aeiou]y$/.test(entity)) return `${entity.slice(0, -1)}ies`;
  if (/(?:s|x|z|ch|sh)$/.test(entity)) return `${entity}es`;
  return `${entity}s`;
}

function refuse(problems: readonly string[], withUsage: boolean): never {
  console.error("✖ nothing was written, because:");
  for (const problem of problems) console.error(`  - ${problem}`);
  if (withUsage) console.error(`\n${usage}`);
  process.exit(1);
}
