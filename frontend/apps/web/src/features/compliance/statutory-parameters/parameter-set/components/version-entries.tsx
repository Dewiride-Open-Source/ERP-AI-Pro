import { SearchedList } from "@/shared/lists/searched/searched-list";
import type { SearchedListView } from "@/shared/lists/searched/searched-list-view";

import type { ParameterColumn, ParameterRow } from "../../parameter-sets/components/set-items";

import { entryColumns, entryLayout, entryRow, entrySearchKeys } from "./cell-text";
import { parameterSetAddress } from "./parameter-set-view";

const entriesPerPage = 10;

const entriesSearch = {
  label: "Code or words",
  advice: "Search by another code or other words.",
} as const;

// The entries keep the version on screen in every address they build (a search, a page, Clear the search), because the
// version is the page's own parameter, not the searched list's.
export function VersionEntries({
  setKey,
  version,
  view,
  columns,
  rows,
  labelledBy,
}: {
  setKey: string;
  version: string | undefined;
  view: SearchedListView;
  columns: readonly ParameterColumn[];
  rows: readonly ParameterRow[];
  labelledBy: string;
}) {
  return (
    <SearchedList
      items={rows}
      pageSize={entriesPerPage}
      keysOf={(row) => entrySearchKeys(columns, row)}
      rowOf={(row) => entryRow(columns, row)}
      address={parameterSetAddress(setKey, version)}
      view={view}
      label="Entries"
      labelledBy={labelledBy}
      noun={{ one: "entry", other: "entries" }}
      search={entriesSearch}
      columns={entryColumns(columns)}
      layout={entryLayout(columns)}
      outerNavigation
    />
  );
}
