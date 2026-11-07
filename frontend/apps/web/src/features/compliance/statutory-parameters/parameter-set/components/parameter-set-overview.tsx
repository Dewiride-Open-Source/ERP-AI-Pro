import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { Empty, EmptyHeader, EmptyTitle } from "@dewiride/erp-ui/components/ui/empty";
import { GitCompareArrowsIcon, ListIcon } from "lucide-react";
import { notFound, redirect } from "next/navigation";

import type { SearchParameters } from "@/shared/lists/list-query";

import { parameterSetsNavigation } from "../../nav";
import { SetBasisBadge } from "../../parameter-sets/components/set-basis-badge";
import type { ParameterSetDetail, ShownVersion } from "../../parameter-sets/components/set-items";
import {
  asOfText,
  changeCounts,
  longDate,
  parameterSetUnavailable,
  versionRange,
} from "../../parameter-sets/components/set-text";
import { getParameterSet } from "../../parameter-sets/server/queries";

import { ParameterSetBrowser } from "./parameter-set-browser";
import { parameterSetTitle } from "./parameter-set-page-title";
import {
  isParameterSetKey,
  keptVersion,
  parameterSetHref,
  readParameterSetView,
  versionHref,
  versionStanding,
  type ParameterSetView,
} from "./parameter-set-view";
import { VersionChanges } from "./version-changes";
import { VersionEntries } from "./version-entries";
import type { VersionItem } from "./version-list";

const versionsHeadingId = "parameter-set-versions-heading";

const entriesHeadingId = "parameter-set-entries-heading";

const changesHeadingId = "parameter-set-changes-heading";

export async function ParameterSetOverview({
  setKey,
  searchParameters,
}: {
  setKey: string;
  searchParameters: SearchParameters;
}) {
  if (!isParameterSetKey(setKey)) notFound();
  const { view, canonical } = readParameterSetView(searchParameters);
  if (!canonical) redirect(parameterSetHref(setKey, view));

  const read = await getParameterSet(setKey, view.version);
  if (read.status === "missing") notFound();
  if (read.status === "failed") return <ParameterSetUnavailable />;

  const { set } = read;
  const version = keptVersion(view.version, set.versions, set.currentFrom);
  if (version !== view.version) redirect(parameterSetHref(setKey, { ...view, version }));

  return (
    <div className="grid min-w-0 grid-cols-1 gap-section" data-testid="parameter-set-overview">
      <header className="grid gap-3">
        <div className="grid gap-1">
          <p className="text-eyebrow text-primary uppercase">{parameterSetsNavigation.title}</p>
          <h1 className="text-title wrap-anywhere">{set.title}</h1>
        </div>
        <div>
          <SetBasisBadge basis={set.basis} />
        </div>
        <p className="max-w-prose text-body text-muted-foreground" data-testid="parameter-set-description">
          {set.description}
        </p>
        <p className="max-w-prose text-body" data-testid="parameter-set-as-of">
          {asOfText(set.asOf)}
        </p>
      </header>

      <ParameterSetBrowser
        versions={{ headingId: versionsHeadingId, focusOnFollow: entriesHeadingId, items: versionItems(set) }}
        entries={<EntriesCard set={set} view={view} />}
        changes={set.shown === null ? null : <ChangesCard set={set} shown={set.shown} />}
      />
    </div>
  );
}

function versionItems(set: ParameterSetDetail): readonly VersionItem[] {
  return set.versions.map((version) => ({
    from: version.from,
    label: versionRange(version),
    href: versionHref(set.key, version.from, set.currentFrom),
    shown: version.from === set.shown?.from,
    standing: versionStanding(version.from, set.currentFrom),
    instruments: version.instruments,
    changes: changeCounts(version),
  }));
}

function lowerFirst(text: string): string {
  return `${text.charAt(0).toLowerCase()}${text.slice(1)}`;
}

function EntriesCard({ set, view }: { set: ParameterSetDetail; view: ParameterSetView }) {
  const { shown } = set;
  const firstVersion = set.versions.at(-1);

  return (
    <Card data-testid="parameter-set-entries">
      <CardHeader>
        <CardTitle>
          <h2 id={entriesHeadingId} tabIndex={-1} className="flex items-center gap-2 outline-none">
            <ListIcon className="size-4 text-muted-foreground" aria-hidden />
            Entries
          </h2>
        </CardTitle>
        <CardDescription>
          {shown === null
            ? "No version of this set is in force today."
            : `The entries of the version ${lowerFirst(versionRange(shown))}, in the order of their keys.`}
        </CardDescription>
      </CardHeader>
      <CardContent>
        {shown === null ? (
          <Empty className="border" data-testid="parameter-set-not-in-force">
            <EmptyHeader>
              <EmptyTitle>
                {firstVersion === undefined
                  ? "This set has no versions."
                  : `Its first version applies from ${longDate(firstVersion.from)}.`}
              </EmptyTitle>
            </EmptyHeader>
          </Empty>
        ) : (
          <VersionEntries
            setKey={set.key}
            version={view.version}
            view={view.list}
            columns={set.columns}
            rows={shown.rows}
            labelledBy={entriesHeadingId}
          />
        )}
      </CardContent>
    </Card>
  );
}

function ChangesCard({ set, shown }: { set: ParameterSetDetail; shown: ShownVersion }) {
  const first = set.versions.at(-1)?.from === shown.from;

  return (
    <Card data-testid="parameter-set-changes">
      <CardHeader>
        <CardTitle>
          <h2 id={changesHeadingId} className="flex items-center gap-2">
            <GitCompareArrowsIcon className="size-4 text-muted-foreground" aria-hidden />
            What changed
          </h2>
        </CardTitle>
        {first ? null : (
          <CardDescription>
            {`What the version ${lowerFirst(versionRange(shown))} added, changed and removed, compared with the version before it.`}
          </CardDescription>
        )}
      </CardHeader>
      <CardContent>
        <VersionChanges columns={set.columns} shown={shown} first={first} />
      </CardContent>
    </Card>
  );
}

function ParameterSetUnavailable() {
  return (
    <div className="grid min-w-0 grid-cols-1 gap-section">
      <header className="grid gap-1">
        <p className="text-eyebrow text-primary uppercase">{parameterSetsNavigation.title}</p>
        <h1 className="text-title">{parameterSetTitle}</h1>
      </header>
      <div
        role="status"
        data-testid="parameter-set-unavailable"
        className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
      >
        <p className="font-medium">{parameterSetUnavailable}</p>
      </div>
    </div>
  );
}
