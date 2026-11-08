import Link from "next/link";
import { redirect } from "next/navigation";

import type { SearchParameters } from "@/shared/lists/list-query";
import { SearchedList } from "@/shared/lists/searched/searched-list";
import type { SearchedListView } from "@/shared/lists/searched/searched-list-view";

import { parameterSetsNavigation } from "../../nav";
import { parameterSetPath } from "../../parameter-set/components/parameter-set-view";
import { listParameterSets, type ParameterSetsRead } from "../server/queries";

import { SetBasisBadge } from "./set-basis-badge";
import type { ParameterSetSummary } from "./set-items";
import {
  currentText,
  nextText,
  parameterSetsAddress,
  parameterSetsHref,
  readParameterSetsView,
  setColumns,
  setSearchKeys,
  setsPerPage,
  setsSearch,
} from "./set-list";
import { entryCountText, parameterSetsDescription, parameterSetsUnavailable } from "./set-text";

const setsHeadingId = "parameter-sets-heading";

export async function ParameterSetsOverview({ searchParameters }: { searchParameters: SearchParameters }) {
  const { title, area } = parameterSetsNavigation;
  const { view, canonical } = readParameterSetsView(searchParameters);
  if (!canonical) redirect(parameterSetsHref(view));

  const read = await listParameterSets();

  return (
    <div className="grid min-w-0 grid-cols-1 gap-section" data-testid="parameter-sets-overview">
      <header className="grid gap-2">
        <div>
          <p className="text-eyebrow text-primary uppercase">{area.title}</p>
          <h1 className="text-title">{title}</h1>
        </div>
        <p className="max-w-prose text-body text-muted-foreground">{parameterSetsDescription}</p>
      </header>

      <ParameterSets read={read} view={view} />
    </div>
  );
}

// The sets are searched and paged like any list, so the page stays one screen's height however many sets the ERP holds.
function ParameterSets({ read, view }: { read: ParameterSetsRead; view: SearchedListView }) {
  if (read.status === "failed") {
    return (
      <div
        role="status"
        data-testid="parameter-sets-unavailable"
        className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
      >
        <p className="font-medium">{parameterSetsUnavailable}</p>
      </div>
    );
  }

  return (
    <section aria-labelledby={setsHeadingId} className="grid min-w-0 gap-4">
      <h2 id={setsHeadingId} className="sr-only">
        Parameter sets
      </h2>
      <SearchedList
        items={read.sets}
        pageSize={setsPerPage}
        keysOf={setSearchKeys}
        rowOf={(set) => ({ id: set.key, cells: setCells(set) })}
        address={parameterSetsAddress}
        view={view}
        label="Parameter sets"
        labelledBy={setsHeadingId}
        noun={{ one: "set", other: "sets" }}
        search={setsSearch}
        columns={setColumns}
      />
    </section>
  );
}

function setCells(set: ParameterSetSummary) {
  return {
    set: (
      <Link
        href={parameterSetPath(set.key)}
        prefetch={false}
        className="rounded-sm wrap-anywhere text-primary underline-offset-4 focus-ring hover:underline"
      >
        {set.title}
      </Link>
    ),
    basis: <SetBasisBadge basis={set.basis} />,
    current: currentText(set),
    next: nextText(set),
    entries: entryCountText(set.entryCount),
  };
}
