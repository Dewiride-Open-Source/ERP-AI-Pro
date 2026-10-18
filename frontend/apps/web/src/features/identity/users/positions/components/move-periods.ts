import { compareIsoDates, isIsoDate } from "@dewiride/erp-ui/lib/calendar-date";

export type MoveTiming = "earlier" | "today" | "later";

export interface MovePeriod<TMove> {
  readonly move: TMove;
  readonly from: string;
  readonly until: string | undefined;
  readonly timing: MoveTiming;
}

// A move holds from its date until the day before the next one starts, and the latest holds from its date on, so the one
// that holds today is the latest that started on or before today.
export function movePeriods<TMove>(
  moves: readonly TMove[],
  startOf: (move: TMove) => string | undefined,
  today: string,
): readonly MovePeriod<TMove>[] {
  const dated = moves
    .flatMap((move) => {
      const from = startOf(move);
      return from !== undefined && isIsoDate(from) ? [{ move, from }] : [];
    })
    .sort((first, second) => compareIsoDates(second.from, first.from));
  return dated.map(({ move, from }, index) => {
    const next = dated[index - 1];
    const until = next === undefined ? undefined : dayBefore(next.from);
    return { move, from, until, timing: timingOf(from, until, today) };
  });
}

export function dayBefore(isoDate: string): string {
  const day = new Date(`${isoDate}T00:00:00Z`);
  day.setUTCDate(day.getUTCDate() - 1);
  return day.toISOString().slice(0, 10);
}

function timingOf(from: string, until: string | undefined, today: string): MoveTiming {
  if (compareIsoDates(from, today) > 0) return "later";
  return until === undefined || compareIsoDates(until, today) >= 0 ? "today" : "earlier";
}
