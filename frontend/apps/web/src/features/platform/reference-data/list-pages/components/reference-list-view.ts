import { searchText } from "@dewiride/erp-ui/lib/search/search-text";

export type SearchParameters = Readonly<Record<string, string | readonly string[] | undefined>>;

export interface ReferenceListView {
  readonly search: string | undefined;
  readonly page: number;
}

export interface ReadReferenceListView {
  readonly view: ReferenceListView;
  readonly canonical: boolean;
}

export interface SearchKeys {
  readonly code: string;
  readonly name: string;
}

export interface ReferenceListPage {
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly pageCount: number;
}

export interface ShownReferenceList<T> {
  readonly items: readonly T[];
  readonly page: ReferenceListPage;
}

export interface ReferenceListColumn {
  readonly id: string;
  readonly header: string;
  readonly title?: boolean | undefined;
}

export interface ReferenceListRow {
  readonly id: string;
  readonly cells: Readonly<Record<string, string>>;
}

export const referenceListParameters = { search: "q", page: "page" } as const;

export const searchMaxLength = 100;

const pageNumber = /^[1-9][0-9]{0,5}$/;

const knownParameters: ReadonlySet<string> = new Set(Object.values(referenceListParameters));

const wordSeparator = /\s+/u;

function single(value: string | readonly string[] | undefined): string | undefined {
  return typeof value === "string" ? value : undefined;
}

export function readSearch(text: string): string | undefined {
  const search = text.trim();
  return search === "" || search.length > searchMaxLength ? undefined : search;
}

// The address holds the view, and an address that says it another way is answered with the one canonical address: a
// repeated, malformed or unknown parameter is dropped, the search loses the space around it and the first page is never
// named.
export function readReferenceListView(parameters: SearchParameters): ReadReferenceListView {
  const search = single(parameters[referenceListParameters.search]);
  const page = single(parameters[referenceListParameters.page]);
  const view: ReferenceListView = {
    search: search === undefined ? undefined : readSearch(search),
    page: page !== undefined && pageNumber.test(page) ? Number(page) : 1,
  };
  const canonical =
    Object.entries(parameters).every(
      ([name, value]) => value === undefined || (knownParameters.has(name) && typeof value === "string"),
    ) &&
    search === view.search &&
    (page === undefined || (page === String(view.page) && view.page > 1));
  return { view, canonical };
}

export function referenceListHref<TPath extends string>(
  path: TPath,
  view: ReferenceListView,
): TPath | `${TPath}?${string}` {
  const search = new URLSearchParams();
  if (view.search !== undefined) search.set(referenceListParameters.search, view.search);
  if (view.page > 1) search.set(referenceListParameters.page, String(view.page));
  const query = search.toString();
  return query === "" ? path : `${path}?${query}`;
}

// Every word typed must appear in the code or in the name, compared without case or accents, so "korea republic" finds
// "Korea, Republic of"; a code typed in full comes first, so IN puts India ahead of every name that holds "in".
export function searchReferenceList<T>(
  items: readonly T[],
  search: string | undefined,
  keysOf: (item: T) => SearchKeys,
): readonly T[] {
  const typed = searchText(search ?? "");
  if (typed === "") return items;
  const words = typed.split(wordSeparator);
  const codeMatches: T[] = [];
  const otherMatches: T[] = [];
  for (const item of items) {
    const keys = keysOf(item);
    const code = searchText(keys.code);
    const name = searchText(keys.name);
    if (code === typed) codeMatches.push(item);
    else if (words.every((word) => code.includes(word) || name.includes(word))) otherMatches.push(item);
  }
  return [...codeMatches, ...otherMatches];
}

export function referenceListPage<T>(
  items: readonly T[],
  page: number,
  pageSize: number,
): ShownReferenceList<T> {
  const pageCount = Math.max(1, Math.ceil(items.length / pageSize));
  const shownPage = Math.min(Math.max(1, page), pageCount);
  const start = (shownPage - 1) * pageSize;
  return {
    items: items.slice(start, start + pageSize),
    page: { page: shownPage, pageSize, totalCount: items.length, pageCount },
  };
}

export function shownReferenceList<T>(
  items: readonly T[],
  view: ReferenceListView,
  pageSize: number,
  keysOf: (item: T) => SearchKeys,
): ShownReferenceList<T> {
  return referenceListPage(searchReferenceList(items, view.search, keysOf), view.page, pageSize);
}
