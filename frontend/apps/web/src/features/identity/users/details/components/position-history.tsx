import type { PositionResponse } from "@dewiride/erp-api-client";
import { Badge } from "@dewiride/erp-ui/components/ui/badge";
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { ArrowRightLeftIcon } from "lucide-react";

import { isoDateOf } from "@/shared/api/dates/date-only";

import type { OrganisationOption } from "../../organisation/components/organisation-options";
import { movePeriods, type MovePeriod, type MoveTiming } from "../../positions/components/move-periods";
import { movePeriodTitle, moveTimingLabel } from "../../positions/components/move-text";
import { RecordMoveDialog } from "../../positions/components/record-move-dialog";
import { WithdrawMoveAction } from "../../positions/components/withdraw-move-action";

export type MovesRead =
  | { readonly status: "read"; readonly moves: readonly PositionResponse[] }
  | { readonly status: "failed"; readonly reason: string };

export type MoveChoicesRead =
  | {
      readonly status: "read";
      readonly departments: readonly OrganisationOption[];
      readonly designations: readonly OrganisationOption[];
    }
  | { readonly status: "failed"; readonly reason: string };

const movesHeadingId = "person-moves-heading";

const noneHeld = "None";

export function PositionHistory({
  personId,
  personName,
  moves,
  choices,
  today,
  canWithdraw,
}: {
  personId: string;
  personName: string;
  moves: MovesRead;
  choices: MoveChoicesRead | undefined;
  today: string;
  canWithdraw: boolean;
}) {
  return (
    <Card data-testid="person-moves">
      <CardHeader>
        <CardTitle>
          <h2 id={movesHeadingId} tabIndex={-1} className="flex items-center gap-2 outline-none">
            <ArrowRightLeftIcon className="size-4 text-muted-foreground" aria-hidden />
            Moves
          </h2>
        </CardTitle>
        <CardDescription>
          Where {personName} works and whom they report to, from each date until their next move.
        </CardDescription>
        {choices?.status === "read" ? (
          <CardAction>
            <RecordMoveDialog
              personId={personId}
              personName={personName}
              departments={choices.departments}
              designations={choices.designations}
            />
          </CardAction>
        ) : null}
      </CardHeader>
      <CardContent className="grid gap-4">
        {choices?.status === "failed" ? (
          <p
            role="status"
            className="text-sm text-muted-foreground"
            data-testid="person-moves-choices-failed"
          >
            A move cannot be recorded now, because the departments and designations could not be read.{" "}
            {choices.reason}
          </p>
        ) : null}
        {moves.status === "read" ? (
          <MoveList
            personId={personId}
            periods={movePeriods(moves.moves, (move) => isoDateOf(move.effectiveFrom), today)}
            canWithdraw={canWithdraw}
          />
        ) : (
          <div
            role="status"
            data-testid="person-moves-unavailable"
            className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
          >
            <p className="font-medium">The moves are not available.</p>
            <p className="mt-1 text-sm text-muted-foreground">{moves.reason}</p>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function MoveList({
  personId,
  periods,
  canWithdraw,
}: {
  personId: string;
  periods: readonly MovePeriod<PositionResponse>[];
  canWithdraw: boolean;
}) {
  if (periods.length === 0) {
    return (
      <p className="text-muted-foreground" data-testid="person-moves-none">
        No moves are recorded yet.
      </p>
    );
  }

  return (
    <ol role="list" aria-labelledby={movesHeadingId} className="grid gap-3">
      {periods.map(({ move, from, until, timing }) => (
        <li key={move.id ?? from} className="grid gap-3 rounded-lg border p-4" data-testid="person-move">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h3 className="font-medium">{movePeriodTitle(from, until)}</h3>
            <TimingBadge timing={timing} />
          </div>
          <dl className="grid grid-cols-[minmax(0,auto)_minmax(0,1fr)] gap-x-4 gap-y-1">
            <MoveDetail label="Department" value={move.departmentName ?? noneHeld} />
            <MoveDetail label="Designation" value={move.designationName ?? noneHeld} />
            <MoveDetail label="Manager" value={move.managerDisplayName ?? noneHeld} />
          </dl>
          {canWithdraw && move.id ? (
            <WithdrawMoveAction
              personId={personId}
              positionId={move.id}
              from={from}
              focusAfterWithdrawId={movesHeadingId}
            />
          ) : null}
        </li>
      ))}
    </ol>
  );
}

function TimingBadge({ timing }: { timing: MoveTiming }) {
  const label = moveTimingLabel(timing);
  if (label === undefined) return null;
  return timing === "today" ? (
    <Badge variant="outline" className="border-transparent bg-success/10 text-success dark:bg-success/20">
      {label}
    </Badge>
  ) : (
    <Badge variant="outline">{label}</Badge>
  );
}

function MoveDetail({ label, value }: { label: string; value: string }) {
  return (
    <>
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="min-w-0 wrap-anywhere">{value}</dd>
    </>
  );
}
