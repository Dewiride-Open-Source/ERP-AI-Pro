import type { Route } from "next";

export type TrailSource = {
  readonly basePath: Route;
  readonly title: string;
  readonly areaTitle: string;
};

export type TrailStep =
  | { readonly kind: "link"; readonly label: string; readonly href: Route }
  | { readonly kind: "area"; readonly label: string }
  | { readonly kind: "page"; readonly label: string };

export type CurrentPage = "page" | "section";

export const homePath: Route = "/";

export const homeTitle = "Home";

function isWithin(pathname: string, basePath: string): boolean {
  return pathname === basePath || pathname.startsWith(`${basePath}/`);
}

// One entry's base path can hold another's, as People's holds Departments', so an address belongs to the entry with the
// longest base path it is within; the first of two equal paths wins.
function owningEntry<T>(
  pathname: string,
  entries: readonly T[],
  basePathOf: (entry: T) => string,
): T | undefined {
  let owner: T | undefined;
  let ownerLength = -1;
  for (const entry of entries) {
    const basePath = basePathOf(entry);
    if (basePath !== homePath && isWithin(pathname, basePath) && basePath.length > ownerLength) {
      owner = entry;
      ownerLength = basePath.length;
    }
  }
  return owner;
}

export function currentPage(
  pathname: string,
  href: Route,
  entryPaths: readonly Route[],
): CurrentPage | undefined {
  if (pathname === href) return "page";
  if (href === homePath) return undefined;
  return owningEntry(pathname, [href, ...entryPaths], (path) => path) === href ? "section" : undefined;
}

// The trail follows the navigation registry: Home, the area of the entry the page belongs to, which has no page of its
// own, then that entry. A page the registry does not reach shows Home alone, because nothing above it is known.
export function breadcrumbTrail(pathname: string, sources: readonly TrailSource[]): readonly TrailStep[] {
  if (pathname === homePath) return [{ kind: "page", label: homeTitle }];
  const home: TrailStep = { kind: "link", label: homeTitle, href: homePath };
  const source = owningEntry(pathname, sources, (candidate) => candidate.basePath);
  if (source === undefined) return [home];
  const entry: TrailStep =
    pathname === source.basePath
      ? { kind: "page", label: source.title }
      : { kind: "link", label: source.title, href: source.basePath };
  return [home, { kind: "area", label: source.areaTitle }, entry];
}

export function navigationAreas<T extends { readonly area: { readonly id: string; readonly title: string } }>(
  entries: readonly T[],
): readonly { readonly id: string; readonly title: string; readonly entries: readonly T[] }[] {
  const areas: { id: string; title: string; entries: T[] }[] = [];
  for (const entry of entries) {
    const area = areas.find((candidate) => candidate.id === entry.area.id);
    if (area === undefined) areas.push({ id: entry.area.id, title: entry.area.title, entries: [entry] });
    else area.entries.push(entry);
  }
  return areas;
}
