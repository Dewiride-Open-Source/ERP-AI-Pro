import type { ImageName } from "../../checks/lib/image-contents.ts";
import { TextTable } from "./licence-texts.ts";

export type ComponentKind = "runtime" | "nuget" | "npm" | "golang" | "deb" | "apk";

export interface Component {
  readonly purl: string;
  readonly kind: ComponentKind;
  readonly name: string;
  readonly version: string;
  readonly licence: string;
  readonly copyright: readonly string[];
  readonly texts: readonly string[];
  readonly notices: readonly string[];
  readonly inImage: readonly string[];
  readonly source: string;
  readonly supplier?: string;
  readonly detail?: string;
}

export interface ImageInventory {
  readonly image: ImageName;
  readonly base: string;
  readonly system: string;
  readonly components: readonly Component[];
  readonly texts: TextTable;
}

export const noAssertion = "NOASSERTION";

export const byCodeUnits = (a: string, b: string): number => (a < b ? -1 : Number(a > b));

const kindOrder: readonly ComponentKind[] = ["runtime", "nuget", "npm", "golang", "deb", "apk"];

export function sortedComponents(components: readonly Component[]): Component[] {
  const unique = new Map<string, Component>();
  for (const component of components) {
    if (unique.has(component.purl)) throw new Error(`two components share the package URL ${component.purl}`);
    unique.set(component.purl, component);
  }
  return [...unique.values()].sort(
    (a, b) => kindOrder.indexOf(a.kind) - kindOrder.indexOf(b.kind) || byCodeUnits(a.name, b.name) || byCodeUnits(a.version, b.version) || byCodeUnits(a.purl, b.purl),
  );
}

function purlSegment(value: string): string {
  return encodeURIComponent(value).replace(/%2F/gi, "/");
}

export function nugetPurl(name: string, version: string): string {
  return `pkg:nuget/${encodeURIComponent(name)}@${encodeURIComponent(version)}`;
}

export function npmPurl(name: string, version: string): string {
  const [scope, bare] = name.startsWith("@") ? (name.split("/", 2) as [string, string]) : [undefined, name];
  return `pkg:npm/${scope === undefined ? "" : `${encodeURIComponent(scope)}/`}${encodeURIComponent(bare)}@${encodeURIComponent(version)}`;
}

export function golangPurl(path: string, version: string): string {
  return `pkg:golang/${purlSegment(path)}@${encodeURIComponent(version)}`;
}

export function debPurl(distribution: "debian" | "ubuntu", name: string, version: string, architecture: string, release: string): string {
  return `pkg:deb/${distribution}/${encodeURIComponent(name)}@${encodeURIComponent(version)}?arch=${encodeURIComponent(architecture)}&distro=${encodeURIComponent(release)}`;
}

export function apkPurl(name: string, version: string, architecture: string, release: string): string {
  return `pkg:apk/alpine/${encodeURIComponent(name)}@${encodeURIComponent(version)}?arch=${encodeURIComponent(architecture)}&distro=${encodeURIComponent(release)}`;
}

export function genericPurl(name: string, version: string, qualifiers: Record<string, string> = {}): string {
  const query = Object.entries(qualifiers)
    .map(([key, value]) => `${key}=${encodeURIComponent(value)}`)
    .join("&");
  return `pkg:generic/${encodeURIComponent(name)}@${encodeURIComponent(version)}${query === "" ? "" : `?${query}`}`;
}

export function uniqueLines(lines: Iterable<string>): string[] {
  return [...new Set([...lines].map((line) => line.trim()).filter((line) => line.length > 0))];
}
