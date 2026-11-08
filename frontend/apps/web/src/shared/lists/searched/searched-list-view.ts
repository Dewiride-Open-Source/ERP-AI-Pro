import type { DataTablePage } from "@dewiride/erp-ui/components/data-table/data-table";
import { searchText } from "@dewiride/erp-ui/lib/search/search-text";
import type { Route } from "next";
import type { ReactNode } from "react";

import type { SearchParameters } from "../list-query.ts";

export interface SearchedListView {
  readonly search: string | undefined;
  readonly page: number;
}

export interface ReadSearchedListView {
  readonly view: SearchedListView;
  readonly canonical: boolean;
}

export interface SearchedListAddress {
  readonly path: Route;
  readonly keep?: Readonly<Record<string, string>> | undefined;
}

export interface SearchKeys {
  readonly code: string;
  readonly name: string;
}

export interface ShownSearchedList<T> {
  readonly items: readonly T[];
  readonly page: DataTablePage;
}

export interface SearchedListColumn {
  readonly id: string;
  readonly header: string;
  readonly title?: boolean | undefined;
}

// A cell is usually its text; a list whose rows open a page or carry a badge renders those in the cell instead.
export interface SearchedListRow {
  readonly id: string;
  readonly cells: Readonly<Record<string, ReactNode>>;
}

// A list shows its rows as a table on a wide box and as cards on a narrow one, unless its rows hold so many columns of
// sentences that no table could show them legibly, which shows them as cards at every width.
export type SearchedListLayout = "responsive" | "cards";

export const searchedListParameters = { search: "q", page: "page" } as const;

export const searchMaxLength = 100;

const pageNumber = /^[1-9][0-9]{0,5}$/;

const knownParameters: ReadonlySet<string> = new Set(Object.values(searchedListParameters));

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
// named. A parameter the caller names as its own is the caller's to read and to judge.
export function readSearchedListView(
  parameters: SearchParameters,
  callerParameters: readonly string[] = [],
): ReadSearchedListView {
  const search = single(parameters[searchedListParameters.search]);
  const page = single(parameters[searchedListParameters.page]);
  const view: SearchedListView = {
    search: search === undefined ? undefined : readSearch(search),
    page: page !== undefined && pageNumber.test(page) ? Number(page) : 1,
  };
  const canonical =
    Object.entries(parameters).every(
      ([name, value]) =>
        value === undefined ||
        (knownParameters.has(name) ? typeof value === "string" : callerParameters.includes(name)),
    ) &&
    search === view.search &&
    (page === undefined || (page === String(view.page) && view.page > 1));
  return { view, canonical };
}

export function searchedListHref(address: SearchedListAddress, view: SearchedListView): Route {
  const search = new URLSearchParams(address.keep);
  if (view.search !== undefined) search.set(searchedListParameters.search, view.search);
  if (view.page > 1) search.set(searchedListParameters.page, String(view.page));
  const query = search.toString();
  return query === "" ? address.path : `${address.path}?${query}`;
}

// Every word typed must appear in the code or in the name, compared without case or accents, so "korea republic" finds
// "Korea, Republic of"; a code typed in full comes first, so IN puts India ahead of every name that holds "in".
export function searchByCodeOrName<T>(
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

export function searchedListPage<T>(
  items: readonly T[],
  page: number,
  pageSize: number,
): ShownSearchedList<T> {
  const pageCount = Math.max(1, Math.ceil(items.length / pageSize));
  const shownPage = Math.min(Math.max(1, page), pageCount);
  const start = (shownPage - 1) * pageSize;
  return {
    items: items.slice(start, start + pageSize),
    page: { page: shownPage, pageSize, totalCount: items.length, pageCount },
  };
}

export function shownSearchedList<T>(
  items: readonly T[],
  view: SearchedListView,
  pageSize: number,
  keysOf: (item: T) => SearchKeys,
): ShownSearchedList<T> {
  return searchedListPage(searchByCodeOrName(items, view.search, keysOf), view.page, pageSize);
}
