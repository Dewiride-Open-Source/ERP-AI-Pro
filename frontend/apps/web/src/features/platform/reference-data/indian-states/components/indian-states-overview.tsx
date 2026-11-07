import { MapPinnedIcon } from "lucide-react";
import { redirect } from "next/navigation";

import {
  ReferenceListFrame,
  ReferenceListUnavailable,
} from "../../list-pages/components/reference-list-frame";
import { indianStatesPage } from "../../list-pages/components/reference-list-pages";
import {
  readReferenceListView,
  referenceListHref,
  type SearchParameters,
} from "../../list-pages/components/reference-list-view";
import { SearchedReferenceList } from "../../list-pages/components/searched-reference-list";
import { readIndianStates } from "../../reference-lists/server/queries";

import { indianStateColumns, indianStateRow } from "./indian-state-rows";

export const indianStatesTitle = indianStatesPage.title;

const codesPerPage = 10;

const listHeadingId = "indian-states-heading";

export async function IndianStatesOverview({ searchParameters }: { searchParameters: SearchParameters }) {
  const { view, canonical } = readReferenceListView(searchParameters);
  if (!canonical) redirect(referenceListHref(indianStatesPage.path, view));
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
        <SearchedReferenceList
          items={list.items}
          pageSize={codesPerPage}
          keysOf={(state) => ({ code: state.gstCode, name: state.name })}
          rowOf={indianStateRow}
          path={indianStatesPage.path}
          view={view}
          label={indianStatesTitle}
          labelledBy={listHeadingId}
          noun={{ one: "entry", other: "entries" }}
          columns={indianStateColumns}
        />
      ) : (
        <ReferenceListUnavailable list={list} listName="Indian states and union territories" />
      )}
    </ReferenceListFrame>
  );
}
