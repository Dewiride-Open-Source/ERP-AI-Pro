import { spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { join } from "node:path";

import type { ScaffoldModule } from "./registrations/module.ts";
import { paths } from "./registrations/paths.ts";

export type CompletionStep = {
  readonly name: string;
  readonly program: "dotnet" | "node";
  readonly args: readonly string[];
  readonly folder: "backend" | "frontend" | ".";
};

export type StepRunner = (step: CompletionStep) => number;

export type CompletionOutcome = { readonly failed: CompletionStep | undefined; readonly remaining: readonly CompletionStep[] };

export const testHostVariables = ["ERP_TEST_SQL_CONNECTION", "ERP_TEST_BLOB_EMULATOR_HOST"] as const;

const prettierExtensions = [".ts", ".tsx", ".mts", ".mjs", ".js", ".json", ".css", ".md"];
const passablePath = /^[A-Za-z0-9._()[\]/-]+$/;
const plainArgument = /^[A-Za-z0-9._/:=@+-]+$/;

// The lock files come from NuGet and the migration from EF Core, both after the files exist; the contract and the client are
// generated from the running API test host; the formatters run over this run's files alone.
export function completionSteps(module: ScaffoldModule, written: readonly string[]): CompletionStep[] {
  const csharp = passable(written.filter((path) => path.startsWith("backend/") && path.endsWith(".cs")).map((path) => path.slice("backend/".length)));
  const web = passable(written.filter((path) => path.startsWith("frontend/") && prettierExtensions.some((extension) => path.endsWith(extension))).map((path) => path.slice("frontend/".length)));
  return [
    { name: "restore the local .NET tools", program: "dotnet", args: ["tool", "restore"], folder: "backend" },
    { name: "restore the solution, writing the lock files", program: "dotnet", args: ["restore"], folder: "backend" },
    { name: "format the C# files", program: "dotnet", args: ["format", "--no-restore", "--include", ...csharp], folder: "backend" },
    { name: "add the first migration", program: "node", args: ["scripts/ef/ef.ts", "add", `Initial${module.entities.pascal}`, "--context", module.contextKey], folder: "." },
    { name: "refresh the OpenAPI contract and the web client", program: "node", args: ["scripts/api-client/generate.ts"], folder: "." },
    { name: "format the web and end-to-end files", program: "node", args: ["node_modules/prettier/bin/prettier.cjs", "--write", ...web], folder: "frontend" },
  ];
}

export function completionProblems(root: string, environment: NodeJS.ProcessEnv, dotnetVersion: (folder: string) => string | undefined = installedDotnet): string[] {
  const problems = testHostVariables
    .filter((name) => !environment[name])
    .map((name) => `${name} is not set, so the API test host cannot refresh the OpenAPI contract (see docs/guides/testing.md); set it or pass --files-only`);
  if (dotnetVersion(join(root, "backend")) === undefined) problems.push("dotnet cannot run in backend (it is not on the PATH, or the SDK that backend/global.json names is not installed); install it or pass --files-only");
  if (!existsSync(join(root, paths.prettier))) problems.push(`${paths.prettier} is missing; run pnpm install in frontend or pass --files-only`);
  return problems;
}

export function runCompletion(steps: readonly CompletionStep[], run: StepRunner): CompletionOutcome {
  for (const [index, step] of steps.entries()) {
    if (run(step) !== 0) return { failed: step, remaining: steps.slice(index) };
  }

  return { failed: undefined, remaining: [] };
}

export function spawnStep(root: string): StepRunner {
  return (step) => {
    console.log(`\n▶ ${step.name}`);
    const program = step.program === "node" ? process.execPath : "dotnet";
    const result = spawnSync(program, [...step.args], { cwd: join(root, step.folder), stdio: "inherit", shell: false, env: process.env });
    if (result.error !== undefined) console.error(`✖ ${step.program} could not start: ${result.error.message}`);
    return result.status ?? 1;
  };
}

// The lines start and end in the repository root and change folder on lines of their own, so they run as one block in bash
// and in PowerShell alike.
export function commandLines(steps: readonly CompletionStep[]): string[] {
  const lines: string[] = [];
  let folder: CompletionStep["folder"] = ".";
  for (const step of steps) {
    if (step.folder !== folder) {
      if (folder !== ".") lines.push("cd ..");
      if (step.folder !== ".") lines.push(`cd ${step.folder}`);
      folder = step.folder;
    }

    lines.push([step.program, ...step.args].map(quoted).join(" "));
  }

  if (folder !== ".") lines.push("cd ..");
  return lines;
}

// Single quotes keep parentheses and brackets literal in bash and in PowerShell, which escape a quote within them differently.
export function quoted(argument: string): string {
  if (plainArgument.test(argument)) return argument;
  if (argument.includes("'")) throw new Error(`${argument} holds a single quote, which bash and PowerShell escape differently`);
  return `'${argument}'`;
}

function passable(files: readonly string[]): string[] {
  const refused = files.filter((file) => !passablePath.test(file) || file.split("/").includes(".."));
  if (refused.length > 0) throw new Error(`these paths are not passed to a formatter: ${refused.join(", ")}`);
  return [...files];
}

function installedDotnet(folder: string): string | undefined {
  const result = spawnSync("dotnet", ["--version"], { cwd: folder, encoding: "utf8", shell: false });
  return result.error === undefined && result.status === 0 ? result.stdout.trim() : undefined;
}
