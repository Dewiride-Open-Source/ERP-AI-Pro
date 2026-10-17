import type { ChangeRecordResponse } from "@dewiride/erp-api-client";
import { TimelineContent, TimelineItem, TimelineTime } from "@dewiride/erp-ui/components/timeline/timeline";
import { ChevronRightIcon } from "lucide-react";
import { Fragment, type ReactNode } from "react";

import { formatTimeIst } from "@/shared/format/dates";

import {
  changeSentence,
  shownValues,
  snapshotSummary,
  type ShownValue,
  type ShownValues,
} from "./change-text";

export function ChangeEntry({ change, children }: { change: ChangeRecordResponse; children?: ReactNode }) {
  const sentence = changeSentence(change);
  const values = shownValues(change.operation, change.changes ?? []);
  const occurredAt = change.occurredAt;

  return (
    <TimelineItem data-testid="audit-change">
      {occurredAt === null || occurredAt === undefined || Number.isNaN(occurredAt.getTime()) ? null : (
        <TimelineTime dateTime={occurredAt.toISOString()}>{formatTimeIst(occurredAt)}</TimelineTime>
      )}
      <TimelineContent>
        <p data-testid="audit-change-summary" className="wrap-anywhere">
          <span className="font-medium">{sentence.actor}</span> {sentence.action}
          {sentence.subject === undefined ? null : (
            <>
              {" "}
              <span className="font-medium">{sentence.subject}</span>
            </>
          )}
          .
        </p>
        <ChangeValues values={values} />
        {children}
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
