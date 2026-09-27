import type { Matcher } from "react-day-picker";

import {
  calendarNavigationBounds,
  clampIsoDate,
  isIsoDate,
  isoDateToLocalDate,
} from "@dewiride/erp-ui/lib/calendar-date";

export interface CalendarLimitOptions {
  readonly min?: string | undefined;
  readonly max?: string | undefined;
  readonly shown?: string | undefined;
}

export interface CalendarLimits {
  defaultMonth?: Date;
  startMonth?: Date;
  endMonth?: Date;
  disabled: Matcher[];
}

export function calendarLimits(
  today: string,
  { min, max, shown }: CalendarLimitOptions = {},
): CalendarLimits {
  const bounds = calendarNavigationBounds(today, { min, max, selected: shown });
  const defaultMonth = isoDateToLocalDate(
    shown !== undefined && isIsoDate(shown) ? shown : clampIsoDate(today, min, max),
  );
  const startMonth = isoDateToLocalDate(bounds.start);
  const endMonth = isoDateToLocalDate(bounds.end);
  const earliest = min === undefined ? undefined : isoDateToLocalDate(min);
  const latest = max === undefined ? undefined : isoDateToLocalDate(max);

  const limits: CalendarLimits = { disabled: [] };
  if (defaultMonth) limits.defaultMonth = defaultMonth;
  if (startMonth) limits.startMonth = startMonth;
  if (endMonth) limits.endMonth = endMonth;
  if (earliest) limits.disabled.push({ before: earliest });
  if (latest) limits.disabled.push({ after: latest });
  return limits;
}
