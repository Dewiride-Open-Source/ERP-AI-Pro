import type { ChangeRecordResponse } from "@dewiride/erp-api-client";
import {
  Timeline,
  TimelineContent,
  TimelineGroup,
  TimelineItem,
  TimelineTime,
} from "@dewiride/erp-ui/components/timeline/timeline";
import { ChevronRightIcon } from "lucide-react";
import Link from "next/link";
import { Fragment } from "react";

import { formatTimeIst } from "@/shared/format/dates";
import { filterSignature, listHref, withFilters, type ListQuery } from "@/shared/lists/list-query";

import { auditNavigation } from "../../nav";
import { changesList } from "../lists/changes.list";

import { changeDays } from "./change-days";
import {
  actorChangesLinkText,
  changeSentence,
  recordHistoryLinkText,
  shownValues,
  snapshotSummary,
  type ShownValue,
  type ShownValues,
} from "./change-text";

const linkClass = "rounded-sm text-primary underline-offset-4 focus-ring hover:underline";

export function ChangeTimeline({
  changes,
  query,
  labelledBy,
}: {
  changes: readonly ChangeRecordResponse[];
  query: ListQuery;
  labelledBy: string;
}) {
  const current = filterSignature(query.filters, changesList).toLowerCase();

  return (
    <Timeline label="Changes" labelledBy={labelledBy}>
      {changeDays(changes, (change) => change.occurredAt).map((day) => (
        <TimelineGroup key={day.key} heading={day.heading}>
          {day.changes.map((change, index) => (
            <ChangeItem
              key={change.id ?? `${day.key}-${index}`}
              change={change}
              query={query}
              current={current}
            />
          ))}
        </TimelineGroup>
      ))}
    </Timeline>
  );
}

// A link that would show the list as it is already filtered is left out, so each change offers only views that differ from
// the one on screen.
function ChangeItem({
  change,
  query,
  current,
}: {
  change: ChangeRecordResponse;
  query: ListQuery;
  current: string;
}) {
  const { basePath } = auditNavigation;
  const sentence = changeSentence(change);
  const values = shownValues(change.operation, change.changes ?? []);
  const actorHref = linkTo(query, "actor", change.actorId);
  const recordHref = linkTo(query, "record", change.subjectId);
  const occurredAt = change.occurredAt;

  return (
    <TimelineItem data-testid="audit-change">
      {occurredAt === null || occurredAt === undefined || Number.isNaN(occurredAt.getTime()) ? null : (
        <TimelineTime dateTime={occurredAt.toISOString()}>{formatTimeIst(occurredAt)}</TimelineTime>
      )}
      <TimelineContent>
        <p data-testid="audit-change-summary">
          <span className="font-medium">{sentence.actor}</span> {sentence.action}
          {sentence.subject === undefined ? null : (
            <>
              {" "}
              <span className="font-medium break-words">{sentence.subject}</span>
            </>
          )}
          .
        </p>
        <ChangeValues values={values} />
        <div className="flex flex-wrap gap-x-4 gap-y-1">
          {recordHref === undefined || filtersOf(recordHref) === current ? null : (
            <Link href={listHref(basePath, recordHref, changesList)} prefetch={false} className={linkClass}>
              {recordHistoryLinkText(change.subjectName)}
            </Link>
          )}
          {actorHref === undefined || filtersOf(actorHref) === current ? null : (
            <Link href={listHref(basePath, actorHref, changesList)} prefetch={false} className={linkClass}>
              {actorChangesLinkText(change.actorKind, change.actorName)}
            </Link>
          )}
        </div>
      </TimelineContent>
    </TimelineItem>
  );
}

// What a change altered shows at once; the values a record was created with, or had when it was deleted, fold under a single
// line, so a page of new records stays short and each one opens on demand.
function ChangeValues({ values }: { values: ShownValues }) {
  if (values.values.length === 0) return null;
  if (values.reading === "changed") return <ChangedValues values={values.values} />;
  return (
    <details className="group/values" data-testid="audit-change-snapshot">
      <summary className="inline-flex w-fit cursor-pointer list-none items-center gap-1 rounded-sm text-muted-foreground focus-ring hover:text-foreground [&::-webkit-details-marker]:hidden">
        <ChevronRightIcon aria-hidden className="size-4 transition-transform group-open/values:rotate-90" />
        {snapshotSummary(values.reading, values.values.length)}
      </summary>
      <div className="mt-2">
        <ChangedValues values={values.values} />
      </div>
    </details>
  );
}

function ChangedValues({ values }: { values: readonly ShownValue[] }) {
  return (
    <dl
      className="grid grid-cols-[minmax(0,auto)_minmax(0,1fr)] gap-x-4 gap-y-1"
      data-testid="audit-change-values"
    >
      {values.map((value, index) => (
        <Fragment key={`${value.label}-${index}`}>
          <dt className="text-muted-foreground">{value.label}</dt>
          <dd className="min-w-0 break-words">
            {value.value}
            {value.was === undefined ? null : (
              <>
                {" "}
                <span className="text-muted-foreground">(was {value.was})</span>
              </>
            )}
          </dd>
        </Fragment>
      ))}
    </dl>
  );
}

function linkTo(
  query: ListQuery,
  parameter: "actor" | "record",
  id: string | null | undefined,
): ListQuery | undefined {
  return id === null || id === undefined || id === "" ? undefined : withFilters(query, { [parameter]: id });
}

function filtersOf(query: ListQuery): string {
  return filterSignature(query.filters, changesList).toLowerCase();
}
