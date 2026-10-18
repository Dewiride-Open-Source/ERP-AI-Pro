import type { ChangeRecordResponse } from "@dewiride/erp-api-client";
import { Timeline, TimelineGroup } from "@dewiride/erp-ui/components/timeline/timeline";

import { ListLink } from "@/shared/lists/list-navigation";
import { filterSignature, listHref, withFilters, type ListQuery } from "@/shared/lists/list-query";

import { auditNavigation } from "../../nav";
import { changesList } from "../lists/changes.list";

import { changeDays } from "./change-days";
import { ChangeEntry } from "./change-entry";
import { actorChangesLinkText, recordHistoryLinkText } from "./change-text";

const linkClass = "rounded-sm wrap-anywhere text-primary underline-offset-4 focus-ring hover:underline";

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
              headingId={labelledBy}
            />
          ))}
        </TimelineGroup>
      ))}
    </Timeline>
  );
}

// A link that would show the list as it is already filtered is left out, so each change offers only views that differ from
// the one on screen, and a followed link is missing from the view it opens: focus moves to the timeline's heading instead.
function ChangeItem({
  change,
  query,
  current,
  headingId,
}: {
  change: ChangeRecordResponse;
  query: ListQuery;
  current: string;
  headingId: string;
}) {
  const { basePath } = auditNavigation;
  const actorHref = linkTo(query, "actor", change.actorId);
  const recordHref = linkTo(query, "record", change.subjectId);

  return (
    <ChangeEntry change={change}>
      <div className="flex flex-wrap gap-x-4 gap-y-1">
        {recordHref === undefined || filtersOf(recordHref) === current ? null : (
          <ChangeLink
            href={listHref(basePath, recordHref, changesList)}
            text={recordHistoryLinkText(change.subjectName)}
            headingId={headingId}
          />
        )}
        {actorHref === undefined || filtersOf(actorHref) === current ? null : (
          <ChangeLink
            href={listHref(basePath, actorHref, changesList)}
            text={actorChangesLinkText(change.actorKind, change.actorName)}
            headingId={headingId}
          />
        )}
      </div>
    </ChangeEntry>
  );
}

function ChangeLink({ href, text, headingId }: { href: string; text: string; headingId: string }) {
  return (
    <ListLink href={href} disabled={false} aria-label={text} className={linkClass} focusOnFollow={headingId}>
      {text}
    </ListLink>
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
