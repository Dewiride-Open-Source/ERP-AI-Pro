import { createHash } from "node:crypto";

import { type FileSystemEntry, finalFileSystem, type ImageArchive, type ImageName, imageRepository, repositoryUrl, vendor } from "../../checks/lib/image-contents.ts";
import { shippedPackages } from "./dotnet.ts";
import { goBuildInfo } from "./go.ts";
import { byCodeUnits, noAssertion } from "./inventory.ts";
import { installedAlpinePackages, installedDebianPackages } from "./os-packages.ts";
import type { NoticesDocument, NoticesEntry } from "./render.ts";

export const noticesPath = "usr/share/doc/erp-ai-pro/third-party-notices.json";

const dependencyFile = /^app\/(?:probe\/|migrator\/)?[^/]+\.deps\.json$/;
const npmManifest = /(?:^|\/)node_modules\/((?:@[^/]+\/)?[^/@.][^/]*)\/package\.json$/;
const caddyBinary = "usr/bin/caddy";

export const isInventoryEvidence = (path: string): boolean =>
  path === noticesPath || path === "var/lib/dpkg/status" || path === "lib/apk/db/installed" || path === caddyBinary || dependencyFile.test(path) || (path.startsWith("app/") && npmManifest.test(path));

export type ObservedImage = {
  readonly deb: ReadonlyMap<string, string>;
  readonly apk: ReadonlyMap<string, string>;
  readonly nuget: ReadonlySet<string>;
  readonly npm: ReadonlySet<string>;
  readonly golang: ReadonlyMap<string, string> | undefined;
  readonly goVersion: string | undefined;
  readonly env: Readonly<Record<string, string>>;
};

const textOf = (entry: FileSystemEntry | undefined): string | undefined => (entry?.content === undefined ? undefined : new TextDecoder().decode(entry.content));

export function observedImage(files: ReadonlyMap<string, FileSystemEntry>, env: Readonly<Record<string, string>>): ObservedImage {
  const status = textOf(files.get("var/lib/dpkg/status"));
  const apk = textOf(files.get("lib/apk/db/installed"));
  const nuget = new Set<string>();
  const npm = new Set<string>();
  for (const [path, entry] of files) {
    if (dependencyFile.test(path)) for (const item of shippedPackages(textOf(entry) ?? "{}")) nuget.add(`${item.name}/${item.version}`.toLowerCase());
    const manifest = path.startsWith("app/") ? npmManifest.exec(path) : null;
    if (manifest !== null && entry.content !== undefined) {
      const { name, version } = JSON.parse(textOf(entry) as string) as { name?: unknown; version?: unknown };
      if (typeof name === "string" && typeof version === "string" && name === manifest[1] && !name.startsWith("@dewiride/")) npm.add(`${name}@${version}`);
    }
  }
  const binary = files.get(caddyBinary)?.content;
  const info = binary === undefined ? undefined : goBuildInfo(binary);
  return {
    deb: new Map((status === undefined ? [] : installedDebianPackages(status)).map((item) => [item.name, item.version])),
    apk: new Map((apk === undefined ? [] : installedAlpinePackages(apk)).map((item) => [item.name, item.version])),
    nuget,
    npm,
    golang: info === undefined ? undefined : new Map(info.dependencies.map((module) => [module.path, module.version])),
    goVersion: info?.goVersion.replace(/^go/, ""),
    env,
  };
}

function compareSets(label: string, observed: ReadonlySet<string>, listed: ReadonlySet<string>, both: boolean): string[] {
  return [
    ...[...observed].filter((item) => !listed.has(item)).map((item) => `the image holds the ${label} ${item}, which its notices do not list`),
    ...(both ? [...listed].filter((item) => !observed.has(item)).map((item) => `the notices list the ${label} ${item}, which the image does not hold`) : []),
  ].sort(byCodeUnits);
}

const runtimeVersions: Readonly<Record<string, string>> = {
  ".NET Runtime": "DOTNET_VERSION",
  "ASP.NET Core Runtime": "ASPNET_VERSION",
  "Node.js": "NODE_VERSION",
  yarn: "YARN_VERSION",
  Caddy: "CADDY_VERSION",
};

export function inventoryProblems(image: ImageName, document: NoticesDocument, observed: ObservedImage): string[] {
  if (document.image !== image) return [`/${noticesPath} describes the ${document.image} image, not ${image}`];
  const entries = Object.values(document.components);
  const listed = (kind: NoticesEntry["kind"], key: (entry: NoticesEntry) => string) => new Set(entries.filter((entry) => entry.kind === kind).map(key));
  const goModules = new Set(entries.filter((entry) => entry.kind === "golang" && entry.name !== "Go standard library").map((entry) => `${entry.name}@${entry.version}`));
  const stdlib = entries.find((entry) => entry.kind === "golang" && entry.name === "Go standard library");
  return [
    ...compareSets("dpkg package", new Set([...observed.deb].map(([name, version]) => `${name} ${version}`)), listed("deb", (entry) => `${entry.name} ${entry.version}`), true),
    ...compareSets("apk package", new Set([...observed.apk].map(([name, version]) => `${name} ${version}`)), listed("apk", (entry) => `${entry.name} ${entry.version}`), true),
    ...compareSets("NuGet package", observed.nuget, listed("nuget", (entry) => `${entry.name}/${entry.version}`.toLowerCase()), true),
    ...compareSets("npm package", observed.npm, listed("npm", (entry) => `${entry.name}@${entry.version}`), false),
    ...compareSets("Go module", new Set([...(observed.golang ?? new Map())].map(([path, version]) => `${path}@${version}`)), goModules, true),
    ...(observed.goVersion !== undefined && stdlib?.version !== observed.goVersion ? [`/usr/bin/caddy was built with Go ${observed.goVersion}, but the notices list the Go standard library ${stdlib?.version ?? "(none)"}`] : []),
    ...entries
      .filter((entry) => entry.kind === "runtime" && runtimeVersions[entry.name] !== undefined)
      .flatMap((entry) => {
        const variable = runtimeVersions[entry.name] as string;
        const value = observed.env[variable];
        return value === undefined || value.replace(/^v/, "") === entry.version.replace(/^v/, "") ? [] : [`the image sets ${variable}=${value}, but the notices list ${entry.name} ${entry.version}`];
      }),
  ];
}

export const imageEnvironment = (archive: ImageArchive): Record<string, string> =>
  Object.fromEntries((archive.config.Env ?? []).map((entry) => [entry.slice(0, entry.indexOf("=")), entry.slice(entry.indexOf("=") + 1)]));

export function imageInventoryProblems(image: ImageName, archive: ImageArchive): string[] {
  const files = finalFileSystem(archive.layers);
  const notices = files.get(noticesPath)?.content;
  if (notices === undefined) return [`the image has no /${noticesPath}`];
  return inventoryProblems(image, JSON.parse(new TextDecoder().decode(notices)) as NoticesDocument, observedImage(files, imageEnvironment(archive)));
}

export type SpdxInput = {
  readonly image: ImageName;
  readonly version: string;
  readonly created: string;
  readonly configDigest: string;
  readonly licence: string;
  readonly document: NoticesDocument;
};

export const spdxTool = "erp-ai-pro-notices";

const spdxIdOf = (purl: string): string => `SPDXRef-Package-${createHash("sha256").update(purl).digest("hex").slice(0, 16)}`;

export function spdxCreated(created: string): string {
  const time = new Date(created);
  if (Number.isNaN(time.getTime())) throw new Error(`the image's creation time "${created}" is not a time`);
  return time.toISOString().replace(/\.\d{3}Z$/, "Z");
}

export function spdxDocument(input: SpdxInput): Record<string, unknown> {
  const repository = imageRepository(input.image);
  const digest = input.configDigest.replace(/^sha256:/, "");
  const packages = Object.entries(input.document.components).map(([purl, entry]) => ({
    SPDXID: spdxIdOf(purl),
    name: entry.name,
    versionInfo: entry.version,
    ...(entry.supplier === undefined ? {} : { supplier: entry.supplier }),
    downloadLocation: entry.source === "" ? noAssertion : entry.source,
    filesAnalyzed: false,
    licenseConcluded: noAssertion,
    licenseDeclared: entry.spdx,
    copyrightText: entry.copyright.length === 0 ? noAssertion : entry.copyright.join("\n"),
    ...(entry.detail === undefined ? {} : { comment: entry.detail }),
    externalRefs: [{ referenceCategory: "PACKAGE-MANAGER", referenceType: "purl", referenceLocator: purl }],
  }));
  const identifiers = new Set(packages.map((item) => item.SPDXID));
  if (identifiers.size !== packages.length) throw new Error("two package URLs share an SPDX identifier");
  return {
    spdxVersion: "SPDX-2.3",
    dataLicense: "CC0-1.0",
    SPDXID: "SPDXRef-DOCUMENT",
    name: `${repository}:${input.version}`,
    documentNamespace: `${repositoryUrl}/spdx/${input.image}/${encodeURIComponent(input.version)}/${digest}`,
    creationInfo: { created: spdxCreated(input.created), creators: [`Organization: ${vendor}`, `Tool: ${spdxTool}`] },
    packages: [
      {
        SPDXID: "SPDXRef-Image",
        name: repository,
        versionInfo: input.version,
        supplier: `Organization: ${vendor}`,
        downloadLocation: /^\d+\.\d+\.\d+$/.test(input.version) ? `${repositoryUrl}/releases/tag/v${input.version}` : noAssertion,
        filesAnalyzed: false,
        licenseConcluded: noAssertion,
        licenseDeclared: input.licence,
        copyrightText: input.document.copyright,
        primaryPackagePurpose: "CONTAINER",
        comment: `the container image whose configuration digest is ${input.configDigest}, built on ${input.document.base}`,
      },
      ...packages,
    ],
    relationships: [
      { spdxElementId: "SPDXRef-DOCUMENT", relationshipType: "DESCRIBES", relatedSpdxElement: "SPDXRef-Image" },
      ...packages.map((item) => ({ spdxElementId: "SPDXRef-Image", relationshipType: "CONTAINS", relatedSpdxElement: item.SPDXID })),
    ],
  };
}
