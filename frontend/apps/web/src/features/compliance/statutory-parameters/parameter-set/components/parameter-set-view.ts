import { isIsoDate } from "@dewiride/erp-ui/lib/calendar-date";
import type { Route } from "next";

import type { SearchParameters } from "@/shared/lists/list-query";
import {
  readSearchedListView,
  searchedListHref,
  type SearchedListAddress,
  type SearchedListView,
} from "@/shared/lists/searched/searched-list-view";

export type VersionStanding = "inForceToday" | "upcoming" | "past";

export interface ParameterSetView {
  readonly version: string | undefined;
  readonly list: SearchedListView;
}

export interface ReadParameterSetView {
  readonly view: ParameterSetView;
  readonly canonical: boolean;
}

const versionParameter = "version";

const setKeyMaxLength = 64;

// The API's own rule for a set's key (GetParameterSetRequest), checked here first so an address no set can have answers
// not found without a request.
const setKeyPattern = /^[a-z0-9]+(?:-[a-z0-9]+)*(?:\.[a-z0-9]+(?:-[a-z0-9]+)*)+$/;

export function isParameterSetKey(text: string): boolean {
  return text.length <= setKeyMaxLength && setKeyPattern.test(text);
}

// The API reads a date as .NET's DateOnly, which has no year 0.
function isVersionDate(text: string): boolean {
  return isIsoDate(text) && !text.startsWith("0000");
}

// The version is the page's own parameter beside the searched list's: a repeated or malformed one is dropped, and whether
// a well-formed one names a version of the set, and not the one in force today, is known only once the set is read
// (keptVersion).
export function readParameterSetView(parameters: SearchParameters): ReadParameterSetView {
  const { view: list, canonical: listCanonical } = readSearchedListView(parameters, [versionParameter]);
  const requested = parameters[versionParameter];
  const version = typeof requested === "string" && isVersionDate(requested) ? requested : undefined;
  return {
    view: { version, list },
    canonical: listCanonical && requested === version,
  };
}

// The version in force today is the page's default, so its address leaves the version out, as it does a day that starts
// no version of the set.
export function keptVersion(
  version: string | undefined,
  versions: readonly { readonly from: string }[],
  currentFrom: string | null,
): string | undefined {
  if (version === undefined || version === currentFrom) return undefined;
  return versions.some((candidate) => candidate.from === version) ? version : undefined;
}

// A set whose first version is still to come has none in force today, so every version it has is upcoming.
export function versionStanding(from: string, currentFrom: string | null): VersionStanding {
  if (from === currentFrom) return "inForceToday";
  return currentFrom === null || from > currentFrom ? "upcoming" : "past";
}

export function parameterSetPath(key: string): Route {
  return `/compliance/statutory-parameters/parameter-sets/${key}` as Route;
}

export function parameterSetAddress(key: string, version: string | undefined): SearchedListAddress {
  return {
    path: parameterSetPath(key),
    keep: version === undefined ? undefined : { [versionParameter]: version },
  };
}

export function parameterSetHref(key: string, view: ParameterSetView): Route {
  return searchedListHref(parameterSetAddress(key, view.version), view.list);
}

export function versionHref(key: string, from: string, currentFrom: string | null): Route {
  return parameterSetHref(key, {
    version: from === currentFrom ? undefined : from,
    list: { search: undefined, page: 1 },
  });
}
