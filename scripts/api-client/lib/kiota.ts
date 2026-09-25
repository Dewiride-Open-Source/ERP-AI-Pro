import { spawnSync } from "node:child_process";
import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { dirname, join, posix, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

export const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
export const backendRoot = join(repoRoot, "backend");
export const documentPath = join(repoRoot, "docs", "openapi", "erp.json");
export const packageRoot = join(repoRoot, "frontend", "packages", "api-client");
export const generatedRoot = join(packageRoot, "src", "generated");

export const clientClassName = "ErpApiClient";
export const clientNamespace = "ErpApi";
export const logFileName = ".kiota.log";
export const lockFileName = "kiota-lock.json";

const jsonSerializer = "@microsoft/kiota-serialization-json.JsonSerializationWriterFactory";
const jsonDeserializer = "@microsoft/kiota-serialization-json.JsonParseNodeFactory";

// System.CommandLine reads a token that starts with '@' as a response file, so the value stays inside the --option=value token.
export function generateArguments(output: string, document = documentPath): string[] {
  return [
    "kiota",
    "generate",
    "--language",
    "typescript",
    "--openapi",
    document,
    "--class-name",
    clientClassName,
    "--namespace-name",
    clientNamespace,
    "--output",
    output,
    "--clean-output",
    "--exclude-backward-compatible",
    "--structured-mime-types",
    "application/json",
    `--serializer=${jsonSerializer}`,
    `--deserializer=${jsonDeserializer}`,
    "--disable-validation-rules",
    "NoServerEntry",
    "--log-level",
    "warning",
  ];
}

export function generate(output: string, document = documentPath): void {
  if (!existsSync(document)) {
    fail(`'${relative(repoRoot, document)}' does not exist. Refresh it with the OpenAPI snapshot test first.`);
  }

  const result = spawnSync("dotnet", generateArguments(output, document), { cwd: backendRoot, stdio: "inherit" });
  if (result.error) fail(`dotnet kiota could not be started (${result.error.message}). Run 'dotnet tool restore' in backend/.`);
  if (result.status !== 0) fail(`dotnet kiota exited with ${result.status}.`);
}

export function readTree(root: string): Map<string, string> {
  const files = new Map<string, string>();
  if (!existsSync(root)) return files;

  for (const file of walk(root)) {
    const key = relative(root, file).split(sep).join(posix.sep);
    if (key === logFileName) continue;
    files.set(key, normalise(key, readFileSync(file, "utf8").replaceAll("\r\n", "\n")));
  }

  return new Map([...files].sort(([a], [b]) => a.localeCompare(b, "en")));
}

// The lock file records the document path relative to the output folder, which differs for every folder kiota writes into.
export function normalise(path: string, content: string): string {
  if (path !== lockFileName) return content;

  const lock = JSON.parse(content) as Record<string, unknown>;
  delete lock.descriptionLocation;

  return JSON.stringify(lock, null, 2);
}

export function compareTrees(expected: Map<string, string>, actual: Map<string, string>): string[] {
  const differences: string[] = [];
  for (const [path, content] of expected) {
    if (!actual.has(path)) differences.push(`${path}: missing`);
    else if (actual.get(path) !== content) differences.push(`${path}: content differs`);
  }
  for (const path of actual.keys()) {
    if (!expected.has(path)) differences.push(`${path}: unexpected`);
  }

  return differences.sort((a, b) => a.localeCompare(b, "en"));
}

export function fail(message: string): never {
  console.error(`✖ ${message}`);
  process.exit(1);
}

function* walk(directory: string): Generator<string> {
  for (const entry of readdirSync(directory, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name, "en"))) {
    const full = join(directory, entry.name);
    if (entry.isDirectory()) yield* walk(full);
    else if (statSync(full).isFile()) yield full;
  }
}
