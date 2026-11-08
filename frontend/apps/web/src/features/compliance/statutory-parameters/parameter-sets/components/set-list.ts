import type { Route } from "next";

import type { SearchParameters } from "@/shared/lists/list-query";
import {
  readSearchedListView,
  searchedListHref,
  type ReadSearchedListView,
  type SearchedListAddress,
  type SearchedListColumn,
  type SearchedListView,
  type SearchKeys,
} from "@/shared/lists/searched/searched-list-view";

import type { ParameterSetSummary } from "./set-items.ts";
import { fromText, nothingInForce } from "./set-text.ts";

export const setsPerPage = 10;

export const noNextChange = "None";

export const setsSearch = {
  label: "Title or words",
  advice: "Search by other words from a set's title or description.",
} as const;

export const setColumns: readonly SearchedListColumn[] = [
  { id: "set", header: "Set", title: true },
  { id: "basis", header: "Kind" },
  { id: "current", header: "In force today" },
  { id: "next", header: "Next change" },
  { id: "entries", header: "Entries in force today" },
];

export const parameterSetsAddress: SearchedListAddress = {
  path: "/compliance/statutory-parameters/parameter-sets" as Route,
};

export function readParameterSetsView(parameters: SearchParameters): ReadSearchedListView {
  return readSearchedListView(parameters);
}

export function parameterSetsHref(view: SearchedListView): Route {
  return searchedListHref(parameterSetsAddress, view);
}

// A set is found by its key and by the words of its title and description, so "rent" finds the TDS sets whose words name it.
export function setSearchKeys(set: ParameterSetSummary): SearchKeys {
  return { code: set.key, name: `${set.title} ${set.description}` };
}

export function currentText(set: ParameterSetSummary): string {
  return set.current === null ? nothingInForce : fromText(set.current.from);
}

export function nextText(set: ParameterSetSummary): string {
  return set.next === null ? noNextChange : fromText(set.next.from);
}
