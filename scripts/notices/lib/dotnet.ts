import { spawnSync } from "node:child_process";
import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";

import { nuspecLicence } from "../../checks/lib/licence-policy.ts";
import { packageDirectory, packageEntry } from "../../checks/lib/nuget-packages.ts";
import type { ImageFiles } from "./container-files.ts";
import { type Component, genericPurl, noAssertion, nugetPurl, uniqueLines } from "./inventory.ts";
import { canonicalText, licenceIds, spdxExpression, splitCopyright, type TextTable, textBase } from "./licence-texts.ts";

export type PublishedHost = { readonly project: string; readonly assembly: string; readonly publishFolder: string; readonly imageFolder: string };

export const publishedHosts: readonly PublishedHost[] = [
  { project: "Hosts/Api/Dewiride.Erp.Host.Api/Dewiride.Erp.Host.Api.csproj", assembly: "Dewiride.Erp.Host.Api", publishFolder: "/app/api", imageFolder: "/app" },
  { project: "Hosts/HealthProbe/Dewiride.Erp.Host.HealthProbe/Dewiride.Erp.Host.HealthProbe.csproj", assembly: "Dewiride.Erp.Host.HealthProbe", publishFolder: "/app/probe", imageFolder: "/app/probe" },
  { project: "Hosts/Migrator/Dewiride.Erp.Host.Migrator/Dewiride.Erp.Host.Migrator.csproj", assembly: "Dewiride.Erp.Host.Migrator", publishFolder: "/app/migrator", imageFolder: "/app/migrator" },
];

export type DependencyFile = {
  readonly targets?: Record<string, Record<string, { readonly runtime?: Record<string, unknown>; readonly native?: Record<string, unknown>; readonly runtimeTargets?: Record<string, unknown>; readonly resources?: Record<string, unknown> }>>;
  readonly libraries?: Record<string, { readonly type?: string }>;
};

export type ShippedPackage = { readonly name: string; readonly version: string };

const realAsset = (path: string): boolean => !/(?:^|\/)_\._$/.test(path);

export function shippedPackages(depsJson: string): ShippedPackage[] {
  const deps = JSON.parse(depsJson) as DependencyFile;
  const targets = Object.values(deps.targets ?? {});
  const shipped = new Map<string, ShippedPackage>();
  for (const [key, library] of Object.entries(deps.libraries ?? {})) {
    if (library.type !== "package") continue;
    const assets = targets.flatMap((target) => {
      const entry = target[key];
      return entry === undefined ? [] : [entry.runtime, entry.native, entry.runtimeTargets, entry.resources].flatMap((group) => Object.keys(group ?? {}));
    });
    if (!assets.some(realAsset)) continue;
    const separator = key.indexOf("/");
    if (separator <= 0) throw new Error(`the deps.json library ${key} is not <name>/<version>`);
    shipped.set(key.toLowerCase(), { name: key.slice(0, separator), version: key.slice(separator + 1) });
  }
  return [...shipped.values()];
}

function run(command: string, args: readonly string[], cwd: string): void {
  const result = spawnSync(command, args, { cwd, encoding: "utf8", maxBuffer: 64 * 1024 * 1024 });
  if (result.error !== undefined) throw new Error(`${command} could not start: ${result.error.message}`);
  if (result.status !== 0) throw new Error(`${command} ${args.join(" ")} exited with ${result.status}\n${`${result.stdout}\n${result.stderr}`.trim().split("\n").slice(-40).join("\n")}`);
}

export function publishHosts(backendRoot: string, output: string): Map<PublishedHost, string> {
  const published = new Map<PublishedHost, string>();
  for (const host of publishedHosts) {
    run("dotnet", ["restore", host.project, "--locked-mode"], backendRoot);
    const folder = join(output, host.assembly);
    run("dotnet", ["publish", host.project, "--configuration", "Release", "--no-restore", "--output", folder, "-p:ContinuousIntegrationBuild=true"], backendRoot);
    published.set(host, readFileSync(join(folder, `${host.assembly}.deps.json`), "utf8"));
  }
  return published;
}

const xmlEntities: Readonly<Record<string, string>> = { "&lt;": "<", "&gt;": ">", "&quot;": '"', "&apos;": "'", "&amp;": "&" };

function xmlElement(xml: string, name: string): string | undefined {
  const value = new RegExp(`<${name}>([^<]*)</${name}>`).exec(xml)?.[1];
  return value === undefined ? undefined : value.replace(/&(?:lt|gt|quot|apos|amp);/g, (entity) => xmlEntities[entity] ?? entity).trim();
}

function xmlAttribute(element: string | undefined, name: string): string | undefined {
  return element === undefined ? undefined : new RegExp(`\\b${name}\\s*=\\s*"([^"]*)"`).exec(element)?.[1];
}

const licenceFile = /^(?:licen[cs]e|copying)(?:\.(?:txt|md))?$/i;
const noticeFile = /^(?:notice|third[-_ ]?party[-_ ]?notices?)(?:\.(?:txt|md))?$/i;

function rootFiles(directory: string, pattern: RegExp): string[] {
  return readdirSync(directory, { withFileTypes: true })
    .filter((entry) => entry.isFile() && pattern.test(entry.name))
    .map((entry) => join(directory, entry.name))
    .sort();
}

function firstLine(text: string): string {
  return (text.split("\n").find((line) => line.trim() !== "") ?? "").trim().replace(/\s+/g, " ");
}

function repositorySource(nuspec: string): string | undefined {
  const element = /<repository\b[^>]*>/.exec(nuspec)?.[0];
  const url = xmlAttribute(element, "url")?.replace(/\.git$/, "");
  const commit = xmlAttribute(element, "commit");
  if (url === undefined || url === "") return undefined;
  return commit !== undefined && /^https:\/\/github\.com\//.test(url) ? `${url}/tree/${commit}` : url;
}

export function nugetComponent(globalFolder: string, item: ShippedPackage, table: TextTable): Component {
  const directory = packageDirectory(globalFolder, item.name, item.version);
  const nuspecPath = packageEntry(directory, `${item.name}.nuspec`);
  if (nuspecPath === undefined) throw new Error(`${item.name} ${item.version} is missing from ${globalFolder}; run "dotnet restore --locked-mode" in backend first`);
  const nuspec = readFileSync(nuspecPath, "utf8");
  const declared = nuspecLicence(nuspec);
  const expression = declared.kind === "expression" ? spdxExpression(declared.expression) : undefined;
  const copyright = [xmlElement(nuspec, "copyright") ?? ""];
  const texts: string[] = [];
  const licencePaths = declared.kind === "file" ? [packageEntry(directory, declared.file)].filter((path): path is string => path !== undefined) : rootFiles(directory, licenceFile);
  if (declared.kind === "file" && licencePaths.length === 0) throw new Error(`${item.name} ${item.version} declares the licence file ${declared.file}, which the package lacks`);
  for (const path of licencePaths) {
    const split = splitCopyright(readFileSync(path, "utf8"));
    copyright.push(...split.copyright);
    if (split.body !== "") texts.push(table.add(expression ?? `${firstLine(split.body)} (${path.slice(directory.length + 1)} of ${item.name})`, split.body));
  }
  if (texts.length === 0 && expression !== undefined) for (const id of licenceIds(expression)) texts.push(table.add(textBase(id), canonicalText(textBase(id))));
  const notices = rootFiles(directory, noticeFile).map((path) => table.add(`${path.slice(directory.length + 1)} of ${item.name} ${item.version}`, readFileSync(path, "utf8").replace(/\r\n?/g, "\n").trim()));
  const repository = repositorySource(nuspec);
  const lower = { name: item.name.toLowerCase(), version: item.version.toLowerCase() };
  const authors = xmlElement(nuspec, "authors");
  return {
    purl: nugetPurl(item.name, item.version),
    kind: "nuget",
    name: item.name,
    version: item.version,
    licence: expression ?? (declared.kind === "url" ? `licence URL ${declared.url}` : noAssertion),
    copyright: uniqueLines(copyright),
    texts: [...new Set(texts)],
    notices: [...new Set(notices)],
    inImage: [],
    source: `https://api.nuget.org/v3-flatcontainer/${lower.name}/${lower.version}/${lower.name}.${lower.version}.nupkg`,
    supplier: authors === undefined || authors === "" ? undefined : `Organization: ${authors}`,
    detail: repository === undefined ? undefined : `built from ${repository}`,
  };
}

export function dotnetRuntimeComponents(files: ImageFiles, env: Readonly<Record<string, string>>): Component[] {
  const licence = files.text("usr/share/dotnet/LICENSE.txt");
  if (licence === undefined || !files.has("usr/share/dotnet/ThirdPartyNotices.txt")) throw new Error("the .NET base image has no /usr/share/dotnet/LICENSE.txt or /usr/share/dotnet/ThirdPartyNotices.txt");
  const copyright = uniqueLines(splitCopyright(licence).copyright);
  const frameworks = [
    { name: ".NET Runtime", version: env.DOTNET_VERSION, framework: "Microsoft.NETCore.App", repository: "dotnet/runtime", purl: "dotnet-runtime" },
    { name: "ASP.NET Core Runtime", version: env.ASPNET_VERSION, framework: "Microsoft.AspNetCore.App", repository: "dotnet/aspnetcore", purl: "aspnetcore-runtime" },
  ];
  return frameworks.map(({ name, version, framework, repository, purl }) => {
    if (version === undefined || !files.list(`usr/share/dotnet/shared/${framework}`).some((entry) => entry.path.endsWith(`/${version}`))) {
      throw new Error(`the .NET base image does not hold ${framework} ${version ?? "(no version set)"}`);
    }
    return {
      purl: genericPurl(purl, version),
      kind: "runtime",
      name,
      version,
      licence: "MIT",
      copyright,
      texts: [],
      notices: [],
      inImage: ["/usr/share/dotnet/LICENSE.txt", "/usr/share/dotnet/ThirdPartyNotices.txt"],
      source: `https://github.com/${repository}/tree/v${version}`,
      detail: `the shared framework ${framework} of the .NET base image; /usr/share/dotnet/ThirdPartyNotices.txt holds the notices of the components it includes`,
    };
  });
}
