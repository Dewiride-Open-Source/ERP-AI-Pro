import { MapPinnedIcon } from "lucide-react";
import { redirect } from "next/navigation";

import type { SearchParameters } from "@/shared/lists/list-query";
import { SearchedList } from "@/shared/lists/searched/searched-list";
import {
  readSearchedListView,
  searchedListHref,
  type SearchedListAddress,
} from "@/shared/lists/searched/searched-list-view";

import {
  ReferenceListFrame,
  ReferenceListUnavailable,
} from "../../list-pages/components/reference-list-frame";
import { indianStatesPage, referenceListSearch } from "../../list-pages/components/reference-list-pages";
import { readIndianStates } from "../../reference-lists/server/queries";

import { indianStateColumns, indianStateRow } from "./indian-state-rows";

export const indianStatesTitle = indianStatesPage.title;

const codesPerPage = 10;

const listHeadingId = "indian-states-heading";

const indianStatesAddress: SearchedListAddress = { path: indianStatesPage.path };

export async function IndianStatesOverview({ searchParameters }: { searchParameters: SearchParameters }) {
  const { view, canonical } = readSearchedListView(searchParameters);
  if (!canonical) redirect(searchedListHref(indianStatesAddress, view));
  const list = await readIndianStates();

  return (
    <ReferenceListFrame
      title={indianStatesTitle}
      description="The GST State code of each state and union territory and the days it applies. A code that no longer applies shows the last day it did."
      listHeading="GST State codes"
      listDescription="By code, then by the first day. Search by a name or by a code."
      headingId={listHeadingId}
      icon={MapPinnedIcon}
      testId="indian-states-card"
    >
      {list.status === "read" ? (
        <SearchedList
          items={list.items}
          pageSize={codesPerPage}
          keysOf={(state) => ({ code: state.gstCode, name: state.name })}
          rowOf={indianStateRow}
          address={indianStatesAddress}
          view={view}
          label={indianStatesTitle}
          labelledBy={listHeadingId}
          noun={{ one: "entry", other: "entries" }}
          search={referenceListSearch}
          columns={indianStateColumns}
        />
      ) : (
        <ReferenceListUnavailable list={list} listName="Indian states and union territories" />
      )}
    </ReferenceListFrame>
  );
}
