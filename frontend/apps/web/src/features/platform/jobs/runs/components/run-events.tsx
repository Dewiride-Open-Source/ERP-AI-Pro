import type { JobRunEventResponse } from "@dewiride/erp-api-client";
import {
  Timeline,
  TimelineContent,
  TimelineGroup,
  TimelineItem,
  TimelineTime,
} from "@dewiride/erp-ui/components/timeline/timeline";

import { formatTimeIst } from "@/shared/format/dates";

import { eventErrorText, eventSentence, retryText, runEventDays, showsReference } from "./run-text";

export function RunEvents({
  events,
  labelledBy,
}: {
  events: readonly JobRunEventResponse[];
  labelledBy: string;
}) {
  const days = runEventDays(events);
  if (days.length === 0) {
    return (
      <p className="text-muted-foreground" data-testid="job-run-no-events">
        Nothing has happened to this run yet.
      </p>
    );
  }

  return (
    <Timeline label="History" labelledBy={labelledBy}>
      {days.map((day) => (
        <TimelineGroup key={day.key} heading={day.heading}>
          {day.events.map((event, index) => (
            <RunEvent key={event.id ?? `${day.key}-${index}`} event={event} />
          ))}
        </TimelineGroup>
      ))}
    </Timeline>
  );
}

function RunEvent({ event }: { event: JobRunEventResponse }) {
  const { occurredAt, retryAt, traceId } = event;
  const error = eventErrorText(event);

  return (
    <TimelineItem data-testid="job-run-event">
      {occurredAt instanceof Date ? (
        <TimelineTime dateTime={occurredAt.toISOString()}>{formatTimeIst(occurredAt)}</TimelineTime>
      ) : null}
      <TimelineContent>
        <p className="wrap-anywhere" data-testid="job-run-event-summary">
          {eventSentence(event)}
        </p>
        {error === undefined ? null : <p className="wrap-anywhere text-destructive">{error}</p>}
        {retryAt instanceof Date ? <p className="text-muted-foreground">{retryText(retryAt)}</p> : null}
        {traceId && showsReference(event.kind) ? (
          <p className="text-sm text-muted-foreground">
            Reference: <span className="font-mono break-all">{traceId}</span>
          </p>
        ) : null}
      </TimelineContent>
    </TimelineItem>
  );
}
