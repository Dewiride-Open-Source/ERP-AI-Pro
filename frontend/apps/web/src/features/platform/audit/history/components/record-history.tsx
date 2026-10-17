import type { PagedResponseOfChangeRecordResponse } from "@dewiride/erp-api-client";
import { Timeline, TimelineGroup } from "@dewiride/erp-ui/components/timeline/timeline";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { HistoryIcon } from "lucide-react";
import Link from "next/link";
import { unstable_rethrow } from "next/navigation";

import { ApiError } from "@/shared/api/problem-details";
import { listApiParameters } from "@/shared/lists/list-api";
import { listHref, readListQuery, type ListQuery } from "@/shared/lists/list-query";

import { changeDays } from "../../changes/components/change-days";
import { ChangeEntry } from "../../changes/components/change-entry";
import { changesList } from "../../changes/lists/changes.list";
import { getChanges } from "../../changes/server/queries";
import { auditNavigation } from "../../nav";

const historyHeadingId = "record-history-heading";

const latestChangeCount = 10;

const counts = new Intl.NumberFormat("en-IN");

type HistoryRead =
  | { readonly status: "read"; readonly changes: PagedResponseOfChangeRecordResponse }
  | { readonly status: "failed"; readonly reason: unknown };

export async function RecordHistory({ recordId, recordName }: { recordId: string; recordName: string }) {
  const { query } = readListQuery({ record: recordId }, changesList);
  if (query.filters.record === undefined) {
    throw new RangeError(`The history of a record needs the record's id; "${recordId}" is not one.`);
  }
  const read = await readLatestChanges(query);

  return (
    <Card data-testid="record-history">
      <CardHeader>
        <CardTitle>
          <h2 id={historyHeadingId} className="flex items-center gap-2">
            <HistoryIcon className="size-4 text-muted-foreground" aria-hidden />
            Latest changes
          </h2>
        </CardTitle>
        <CardDescription>
          The latest changes to {recordName}, newest first. Times are in India Standard Time.
        </CardDescription>
      </CardHeader>
      <CardContent className="grid gap-4">
        {read.status === "read" ? (
          <LatestChanges changes={read.changes} />
        ) : (
          <Unavailable reason={describeFailure(read.reason)} />
        )}
        <Link
          href={listHref(auditNavigation.basePath, query, changesList)}
          className="w-fit rounded-sm text-primary underline-offset-4 focus-ring hover:underline"
          data-testid="record-history-link"
        >
          Show the full history in the audit trail
        </Link>
      </CardContent>
    </Card>
  );
}

function LatestChanges({ changes }: { changes: PagedResponseOfChangeRecordResponse }) {
  const items = changes.items ?? [];
  const totalCount = changes.totalCount ?? items.length;
  if (items.length === 0) {
    return (
      <p className="text-muted-foreground" data-testid="record-history-empty">
        No changes are recorded yet.
      </p>
    );
  }

  return (
    <>
      {totalCount > items.length ? (
        <p className="text-muted-foreground" data-testid="record-history-count">
          The latest {counts.format(items.length)} of {counts.format(totalCount)} changes.
        </p>
      ) : null}
      <Timeline label="Latest changes" labelledBy={historyHeadingId}>
        {changeDays(items, (change) => change.occurredAt).map((day) => (
          <TimelineGroup key={day.key} heading={day.heading}>
            {day.changes.map((change, index) => (
              <ChangeEntry key={change.id ?? `${day.key}-${index}`} change={change} />
            ))}
          </TimelineGroup>
        ))}
      </Timeline>
    </>
  );
}

async function readLatestChanges(query: ListQuery): Promise<HistoryRead> {
  try {
    const changes = await getChanges(
      listApiParameters({ ...query, pageSize: latestChangeCount }, changesList),
    );
    return { status: "read", changes };
  } catch (error) {
    unstable_rethrow(error);
    return { status: "failed", reason: error };
  }
}

function describeFailure(reason: unknown): string {
  return reason instanceof ApiError ? reason.message : "The API did not respond.";
}

function Unavailable({ reason }: { reason: string }) {
  return (
    <div
      role="status"
      data-testid="record-history-unavailable"
      className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
    >
      <p className="font-medium">The changes are not available.</p>
      <p className="mt-1 text-sm text-muted-foreground">{reason}</p>
    </div>
  );
}
