import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, posix } from "node:path";

import { type ImageName, noticesFiles } from "../../checks/lib/image-contents.ts";
import { globalPackagesFolder } from "../../checks/lib/nuget-packages.ts";
import { describeImage, finalBaseImage, type ImageFiles, osRelease, readImageFiles } from "./container-files.ts";
import { dotnetRuntimeComponents, nugetComponent, publishHosts, shippedPackages } from "./dotnet.ts";
import { caddyComponent, caddySourceFiles, goBuildInfo, goModuleComponents, goToolchainComponent } from "./go.ts";
import { type ImageInventory, sortedComponents } from "./inventory.ts";
import { TextTable } from "./licence-texts.ts";
import { nodeRuntimeComponents, webPackageComponents } from "./npm.ts";
import { alpineComponents, debianComponents, type DistributionRelease } from "./os-packages.ts";
import { noticesDocument, renderJson, renderText } from "./render.ts";

export type InventoryOptions = { readonly repoRoot: string; readonly workspace: string };

const smallLegalFile = (path: string): boolean =>
  /^usr\/share\/doc\/[^/]+\/copyright$/.test(path) ||
  path.startsWith("usr/share/common-licenses/") ||
  ["var/lib/dpkg/status", "lib/apk/db/installed", "usr/lib/os-release", "etc/os-release"].includes(path) ||
  /^(?:package\.json|licen[cs]e(?:\.\w+)?|thirdpartynotices\.txt)$/i.test(posix.basename(path));

export function projectCopyright(repoRoot: string): string {
  const line = readFileSync(join(repoRoot, "README.md"), "utf8")
    .split(/\r?\n/)
    .find((candidate) => /^Copyright \(c\) \d{4}(?:-\d{4})? Dewiride Technologies Private Limited\.$/.test(candidate));
  if (line === undefined) throw new Error("README.md states no copyright line of Dewiride Technologies Private Limited");
  return line;
}

function dockerfile(repoRoot: string, image: ImageName): string {
  return finalBaseImage(readFileSync(join(repoRoot, "infra", "docker", `${image}.Dockerfile`), "utf8"));
}

function debianRelease(files: ImageFiles): { release: DistributionRelease; system: string } {
  const release = osRelease(files);
  if (release.ID !== "debian" && release.ID !== "ubuntu") throw new Error(`the base image runs ${release.ID ?? "an unknown system"}, not Debian or Ubuntu`);
  return { release: { id: release.ID, version: release.VERSION_ID ?? "", codename: release.VERSION_CODENAME ?? "" }, system: release.PRETTY_NAME ?? `${release.ID} ${release.VERSION_ID}` };
}

const debianPaths = ["/var/lib/dpkg/status", "/usr/share/doc", "/usr/share/common-licenses", "/usr/lib/os-release", "/etc/os-release"];

async function apiInventory({ repoRoot, workspace }: InventoryOptions): Promise<ImageInventory> {
  const base = dockerfile(repoRoot, "api");
  const table = new TextTable();
  const files = await readImageFiles(base, [...debianPaths, "/usr/share/dotnet"], smallLegalFile);
  const { release, system } = debianRelease(files);
  const backend = join(repoRoot, "backend");
  const published = publishHosts(backend, join(workspace, "publish"));
  const packages = new Map([...published.values()].flatMap(shippedPackages).map((item) => [`${item.name}/${item.version}`.toLowerCase(), item]));
  const globalFolder = globalPackagesFolder(backend);
  return {
    image: "api",
    base,
    system,
    texts: table,
    components: sortedComponents([...dotnetRuntimeComponents(files, describeImage(base).env), ...[...packages.values()].map((item) => nugetComponent(globalFolder, item, table)), ...debianComponents(files, release, table)]),
  };
}

async function webInventory({ repoRoot }: InventoryOptions): Promise<ImageInventory> {
  const base = dockerfile(repoRoot, "web");
  const table = new TextTable();
  const files = await readImageFiles(base, [...debianPaths, "/usr/local/LICENSE", "/usr/local/lib/node_modules", "/opt"], smallLegalFile);
  const { release, system } = debianRelease(files);
  return {
    image: "web",
    base,
    system,
    texts: table,
    components: sortedComponents([...nodeRuntimeComponents(files, describeImage(base).env), ...webPackageComponents(join(repoRoot, "frontend"), table), ...debianComponents(files, release, table)]),
  };
}

async function proxyInventory({ repoRoot }: InventoryOptions): Promise<ImageInventory> {
  const base = dockerfile(repoRoot, "proxy");
  const table = new TextTable();
  const files = await readImageFiles(base, ["/lib/apk/db/installed", "/usr/lib/os-release", "/etc/os-release", "/usr/bin/caddy"], (path) => smallLegalFile(path) || path === "usr/bin/caddy");
  const release = osRelease(files);
  if (release.ID !== "alpine") throw new Error(`the proxy's base image runs ${release.ID ?? "an unknown system"}, not Alpine`);
  const binary = files.get("usr/bin/caddy")?.content;
  if (binary === undefined) throw new Error("the proxy's base image has no /usr/bin/caddy");
  const info = goBuildInfo(binary);
  const version = (describeImage(base).env.CADDY_VERSION ?? "").replace(/^v/, "");
  if (!/^\d+\.\d+\.\d+$/.test(version)) throw new Error("the proxy's base image sets no CADDY_VERSION");
  const caddyModule = info.dependencies.find((module) => module.path === "github.com/caddyserver/caddy/v2");
  if (caddyModule?.version !== `v${version}`) throw new Error(`/usr/bin/caddy is built from github.com/caddyserver/caddy/v2 ${caddyModule?.version ?? "(absent)"}, not the image's CADDY_VERSION v${version}`);
  const sources = await caddySourceFiles(version);
  return {
    image: "proxy",
    base,
    system: release.PRETTY_NAME ?? `Alpine Linux ${release.VERSION_ID}`,
    texts: table,
    components: sortedComponents([caddyComponent(version, sources, info, table), goToolchainComponent(info, table), ...goModuleComponents(info, sources, table), ...alpineComponents(files, release.VERSION_ID ?? "", table)]),
  };
}

export function buildInventory(image: ImageName, options: InventoryOptions): Promise<ImageInventory> {
  switch (image) {
    case "api":
      return apiInventory(options);
    case "web":
      return webInventory(options);
    case "proxy":
      return proxyInventory(options);
  }
}

export const committedNotices = (repoRoot: string): string => join(repoRoot, "infra", "docker", "notices");

export type WrittenNotices = { readonly image: ImageName; readonly components: number; readonly texts: number; readonly files: readonly { readonly path: string; readonly bytes: number }[] };

export async function writeNotices(repoRoot: string, output: string, images: readonly ImageName[]): Promise<WrittenNotices[]> {
  const workspace = mkdtempSync(join(tmpdir(), "erp-notices-"));
  try {
    const copyright = projectCopyright(repoRoot);
    const written: WrittenNotices[] = [];
    for (const image of images) {
      const inventory = await buildInventory(image, { repoRoot, workspace });
      const folder = join(output, image);
      mkdirSync(folder, { recursive: true });
      const document = noticesDocument(inventory, copyright);
      const files = [
        { path: join(folder, noticesFiles[0]), content: renderText(inventory, copyright) },
        { path: join(folder, noticesFiles[1]), content: renderJson(document) },
      ];
      for (const file of files) writeFileSync(file.path, file.content);
      written.push({ image, components: inventory.components.length, texts: Object.keys(document.texts).length, files: files.map(({ path, content }) => ({ path, bytes: Buffer.byteLength(content) })) });
    }
    return written;
  } finally {
    rmSync(workspace, { recursive: true, force: true });
  }
}
