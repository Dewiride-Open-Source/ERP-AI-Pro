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

// Home is current only on the start page itself; an entry is current on its own page and stays marked as the section of
// any page below it.
export function currentPage(pathname: string, href: Route): CurrentPage | undefined {
  if (pathname === href) return "page";
  if (href !== homePath && isWithin(pathname, href)) return "section";
  return undefined;
}

// The trail follows the navigation registry: Home, the entry's area, which has no page of its own, then the entry. A
// page the registry does not reach shows Home alone, because nothing above it is known.
export function breadcrumbTrail(pathname: string, sources: readonly TrailSource[]): readonly TrailStep[] {
  if (pathname === homePath) return [{ kind: "page", label: homeTitle }];
  const home: TrailStep = { kind: "link", label: homeTitle, href: homePath };
  const source = sources.find((candidate) => isWithin(pathname, candidate.basePath));
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
