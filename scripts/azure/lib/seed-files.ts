import { readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

export const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
export const seedDirectory = join(repoRoot, "infra", "appconfig");

export const labels = ["local-dev", "production"] as const;
export type Label = (typeof labels)[number];

export const keyPattern = /^Erp:[A-Z][A-Za-z0-9]*:[A-Z][A-Za-z0-9]*:[A-Z][A-Za-z0-9]*$/;
export const flagPattern = /^Erp\.Modules\.[A-Z][A-Za-z0-9]*\.[A-Z][A-Za-z0-9]*(\.[A-Z][A-Za-z0-9]*)?$/;
export const secretPattern = /^Erp--[A-Z][A-Za-z0-9]*--[A-Z][A-Za-z0-9]*--[A-Z][A-Za-z0-9]*$/;

const bootstrapOnlyPrefix = "Erp:Platform:Configuration:";
const hostLocalKeys = new Set(["Erp:Platform:Host:KnownNetworks", "Erp:Platform:Attachments:EmulatorHost"]);
const scriptOwnedKeys: { script: string; keys: ReadonlySet<string> }[] = [
  { script: "scripts/azure/entra.sh", keys: new Set(["Erp:Platform:Identity:TenantId", "Erp:Platform:Identity:ClientId"]) },
  { script: "scripts/azure/provision.sh", keys: new Set(["Erp:Platform:Attachments:BlobServiceUri"]) },
];
const secretLikeSettingPattern = /(Secret|Password|Pwd|Token|ConnectionString|ApiKey|AccessKey|PrivateKey|Certificate|EncryptionKeys?)$/i;
const controlCharacterPattern = /[\u0000-\u001f\u007f]/;
const azureHostSuffixes: { name: string; suffix: string }[] = [
  { name: "a store endpoint", suffix: ".azconfig.io" },
  { name: "a vault address", suffix: ".vault.azure.net" },
  { name: "an Azure SQL host", suffix: ".database.windows.net" },
  { name: "a blob endpoint", suffix: ".blob.core.windows.net" },
];
const identifierPatterns: { name: string; pattern: RegExp }[] = [
  { name: "a connection string", pattern: /(^|[;\s])(Endpoint|Server|Data Source|AccountKey|SharedAccessKey)=/i },
  { name: "a credential", pattern: /(^|[;\s&?])(Password|Pwd|Secret|ClientSecret|ApiKey|Token|sig)=/i },
  { name: "a GUID", pattern: /\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b/i },
];

function owningScript(key: string): string | undefined {
  return scriptOwnedKeys.find(({ keys }) => keys.has(key))?.script;
}

function carriedIdentifier(value: string): string | undefined {
  const lowered = value.toLowerCase();
  return azureHostSuffixes.find(({ suffix }) => lowered.includes(suffix))?.name ?? identifierPatterns.find(({ pattern }) => pattern.test(value))?.name;
}

export interface FeatureFlagSeed {
  id: string;
  enabled: Record<Label, boolean>;
}

export interface KeyVaultReferenceSeed {
  key: string;
  secret: string;
  labels?: Label[];
}

export function referenceLabels(reference: KeyVaultReferenceSeed): readonly Label[] {
  return reference.labels ?? labels;
}

function isLabelList(value: unknown): value is Label[] {
  return Array.isArray(value) && value.length > 0 && new Set(value).size === value.length && value.every((label) => (labels as readonly unknown[]).includes(label));
}

export interface SeedData {
  defaults: Map<string, string>;
  labelled: Record<Label, Map<string, string>>;
  flags: FeatureFlagSeed[];
  references: KeyVaultReferenceSeed[];
}

export function flatten(value: unknown, prefix = ""): Map<string, string> {
  const flat = new Map<string, string>();
  if (Array.isArray(value)) throw new Error(`${prefix || "<root>"}: arrays are host-local and are never seeded`);
  if (value !== null && typeof value === "object") {
    for (const [name, child] of Object.entries(value)) {
      if (name.includes(":")) throw new Error(`${prefix}${name}: a property name must not contain ':'`);
      for (const [key, leaf] of flatten(child, `${prefix}${name}:`)) flat.set(key, leaf);
    }
    return flat;
  }
  if (prefix === "") throw new Error("<root>: the seed file must hold an object");
  if (typeof value !== "string") {
    throw new Error(`${prefix.slice(0, -1)}: a value must be a JSON string (write ${JSON.stringify(value)} as ${JSON.stringify(String(value))})`);
  }
  flat.set(prefix.slice(0, -1), value);
  return flat;
}

export function readJson(file: string): unknown {
  return JSON.parse(readFileSync(file, "utf8"));
}

export function readSettings(file: string): Map<string, string> {
  return flatten(readJson(file));
}

export function readFeatureFlags(file: string): FeatureFlagSeed[] {
  const document = readJson(file);
  if (document === null || typeof document !== "object" || !Array.isArray((document as { flags?: unknown }).flags)) {
    throw new Error(`${file}: expected { "flags": [...] }`);
  }
  return (document as { flags: FeatureFlagSeed[] }).flags;
}

export function readKeyVaultReferences(file: string): KeyVaultReferenceSeed[] {
  const document = readJson(file);
  if (!Array.isArray(document)) throw new Error(`${file}: expected an array of { key, secret, labels? }`);
  return document as KeyVaultReferenceSeed[];
}

export function readSeedData(directory = seedDirectory): SeedData {
  return {
    defaults: readSettings(join(directory, "defaults.json")),
    labelled: {
      "local-dev": readSettings(join(directory, "local-dev.json")),
      production: readSettings(join(directory, "production.json")),
    },
    flags: readFeatureFlags(join(directory, "feature-flags.json")),
    references: readKeyVaultReferences(join(directory, "key-vault-references.json")),
  };
}

type SettingFile = [string, Map<string, string>];

function settingFiles(data: SeedData): SettingFile[] {
  return [["defaults.json", data.defaults], ...labels.map((label): SettingFile => [`${label}.json`, data.labelled[label]])];
}

function validateSetting(file: string, key: string, value: string, problems: string[]): void {
  if (!keyPattern.test(key)) problems.push(`${file}: key '${key}' is not Erp:<Domain>:<Module>:<Setting>`);
  if (key.startsWith(bootstrapOnlyPrefix)) problems.push(`${file}: key '${key}' is bootstrap-only and must not be seeded`);
  if (hostLocalKeys.has(key)) problems.push(`${file}: key '${key}' is host-local and must not be seeded`);
  const owner = owningScript(key);
  if (owner) problems.push(`${file}: key '${key}' is written by ${owner} and must not be seeded`);
  if (secretLikeSettingPattern.test(key.slice(key.lastIndexOf(":") + 1))) {
    problems.push(`${file}: key '${key}' names a secret; seed it as a Key Vault reference, never as a plain value`);
  }
  if (value.trim() === "") problems.push(`${file}: value of '${key}' is empty; leave the key out instead of seeding an empty value`);
  if (controlCharacterPattern.test(value)) problems.push(`${file}: value of '${key}' contains a control character`);
  const identifier = carriedIdentifier(value);
  if (identifier) problems.push(`${file}: value of '${key}' carries ${identifier}; environment identifiers and credentials stay out of the repository`);
}

function validateOverrides(data: SeedData, problems: string[]): void {
  for (const label of labels) {
    for (const key of data.labelled[label].keys()) {
      if (!data.defaults.has(key)) problems.push(`${label}.json: '${key}' overrides a key that defaults.json does not define`);
    }
  }
}

function validateFlags(flags: FeatureFlagSeed[], problems: string[]): void {
  const expectedLabels = [...labels].sort((a, b) => a.localeCompare(b)).join(",");
  const seen = new Set<string>();
  for (const flag of flags) {
    const id = typeof flag?.id === "string" ? flag.id : "";
    if (!flagPattern.test(id)) problems.push(`feature-flags.json: flag id '${id}' is not Erp.Modules.<Domain>.<Module>[.<Capability>]`);
    if (seen.has(id)) problems.push(`feature-flags.json: flag '${id}' is listed twice`);
    seen.add(id);
    const enabled = flag?.enabled !== null && typeof flag?.enabled === "object" ? flag.enabled : {};
    const declared = Object.keys(enabled).sort((a, b) => a.localeCompare(b)).join(",");
    if (declared !== expectedLabels) problems.push(`feature-flags.json: flag '${id}' must declare exactly the labels ${labels.join(" and ")}`);
    for (const label of labels) {
      if (typeof (enabled as Partial<Record<Label, unknown>>)[label] !== "boolean") problems.push(`feature-flags.json: flag '${id}' needs a boolean for '${label}'`);
    }
  }
}

function validateReferences(references: KeyVaultReferenceSeed[], files: SettingFile[], problems: string[]): void {
  const seen = new Set<string>();
  for (const reference of references) {
    const key = typeof reference?.key === "string" ? reference.key : "";
    const secret = typeof reference?.secret === "string" ? reference.secret : "";
    if (!keyPattern.test(key)) problems.push(`key-vault-references.json: key '${key}' is not Erp:<Domain>:<Module>:<Setting>`);
    if (!secretPattern.test(secret)) problems.push(`key-vault-references.json: secret '${secret}' is not Erp--<Domain>--<Module>--<Name>`);
    const owner = owningScript(key);
    if (owner) problems.push(`key-vault-references.json: key '${key}' is written by ${owner} and must not be seeded`);
    if (reference?.labels !== undefined && !isLabelList(reference.labels)) {
      problems.push(`key-vault-references.json: labels of '${key}' must list ${labels.join(" and/or ")}`);
    }
    if (seen.has(key)) problems.push(`key-vault-references.json: key '${key}' is listed twice`);
    seen.add(key);
    for (const [file, settings] of files) {
      if (settings.has(key)) problems.push(`key-vault-references.json: key '${key}' is also a plain value in ${file}`);
    }
  }
}

export function validateSeedData(data: SeedData): string[] {
  const problems: string[] = [];
  const files = settingFiles(data);
  for (const [file, settings] of files) {
    for (const [key, value] of settings) validateSetting(file, key, value, problems);
  }
  validateOverrides(data, problems);
  validateFlags(data.flags, problems);
  validateReferences(data.references, files, problems);
  return problems;
}

function printTable(rows: string[][]): void {
  for (const row of rows) process.stdout.write(`${row.join("\t")}\n`);
}

function main(argv: string[]): number {
  const [command, file] = argv;
  switch (command) {
    case "settings": {
      if (!file) throw new Error("usage: settings <file>");
      printTable([...readSettings(file)].map(([key, value]) => [key, value]));
      return 0;
    }
    case "flags": {
      if (!file) throw new Error("usage: flags <file>");
      printTable(readFeatureFlags(file).flatMap((flag) => labels.map((label) => [flag.id, label, String(flag.enabled[label])])));
      return 0;
    }
    case "references": {
      if (!file) throw new Error("usage: references <file>");
      printTable(readKeyVaultReferences(file).map((reference) => [reference.key, reference.secret, referenceLabels(reference).join(",")]));
      return 0;
    }
    case "validate": {
      const problems = validateSeedData(readSeedData(file ?? seedDirectory));
      for (const problem of problems) console.error(`  ${problem}`);
      if (problems.length > 0) return 1;
      console.log("appconfig seed files ok");
      return 0;
    }
    default:
      throw new Error("usage: node scripts/azure/lib/seed-files.ts settings|flags|references <file> | validate [<directory>]");
  }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  process.exit(main(process.argv.slice(2)));
}
