import { formatDayIst, istDateKey } from "@/shared/format/dates";

export interface ChangeDay<TChange> {
  readonly key: string;
  readonly heading: string;
  readonly changes: readonly TChange[];
}

export const undatedDayKey = "undated";

export const undatedDayHeading = "Time not recorded";

// A page of the history arrives in time order, newest or oldest first, so the changes of one day in India sit together and the
// days keep the order of the page.
export function changeDays<TChange>(
  changes: readonly TChange[],
  timeOf: (change: TChange) => Date | null | undefined,
): readonly ChangeDay<TChange>[] {
  const days = new Map<string, { readonly heading: string; readonly changes: TChange[] }>();
  for (const change of changes) {
    const time = timeOf(change);
    const dated = time !== null && time !== undefined && !Number.isNaN(time.getTime());
    const key = dated ? istDateKey(time) : undatedDayKey;
    const day = days.get(key);
    if (day === undefined) {
      days.set(key, { heading: dated ? formatDayIst(time) : undatedDayHeading, changes: [change] });
    } else {
      day.changes.push(change);
    }
  }
  return [...days].map(([key, day]) => ({ key, heading: day.heading, changes: day.changes }));
}
