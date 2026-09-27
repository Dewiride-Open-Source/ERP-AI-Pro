export interface CalendarNavigationBounds {
  readonly start: string;
  readonly end: string;
}

export interface CalendarNavigationOptions {
  readonly min?: string | undefined;
  readonly max?: string | undefined;
  readonly selected?: string | undefined;
}

// DateOnly spans 0001-01-01 to 9999-12-31, so a year outside that range can never reach the API.
const firstYear = 1;
const lastYear = 9999;
const calendarYearsBack = 100;
const calendarYearsAhead = 30;

const isoDatePattern = /^(\d{4})-(\d{2})-(\d{2})$/;
const displayDatePattern = /^(\d{1,2})([-/.])(\d{1,2})\2(\d{4})$/;
const compactDatePattern = /^(\d{2})(\d{2})(\d{4})$/;
const typedIsoDatePattern = /^(\d{4})-(\d{1,2})-(\d{1,2})$/;

function isLeapYear(year: number): boolean {
  return (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
}

function daysInMonth(year: number, month: number): number {
  if (month === 2) return isLeapYear(year) ? 29 : 28;
  return [4, 6, 9, 11].includes(month) ? 30 : 31;
}

function isoFromParts(year: number, month: number, day: number): string | null {
  if (year < firstYear || year > lastYear || month < 1 || month > 12) return null;
  if (day < 1 || day > daysInMonth(year, month)) return null;
  return `${String(year).padStart(4, "0")}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
}

function yearOf(isoDate: string): number {
  return Number(isoDate.slice(0, 4));
}

function clampYear(year: number): number {
  return Math.min(lastYear, Math.max(firstYear, year));
}

function paddedYear(year: number): string {
  return String(clampYear(year)).padStart(4, "0");
}

function validIsoDate(text: string | undefined): string | undefined {
  return text !== undefined && isIsoDate(text) ? text : undefined;
}

export function isIsoDate(text: string): boolean {
  const match = isoDatePattern.exec(text);
  return match !== null && isoFromParts(Number(match[1]), Number(match[2]), Number(match[3])) === text;
}

export function parseDisplayDate(text: string): string | null {
  const trimmed = text.trim();

  const display = displayDatePattern.exec(trimmed);
  if (display) return isoFromParts(Number(display[4]), Number(display[3]), Number(display[1]));

  const compact = compactDatePattern.exec(trimmed);
  if (compact) return isoFromParts(Number(compact[3]), Number(compact[2]), Number(compact[1]));

  const typedIso = typedIsoDatePattern.exec(trimmed);
  if (typedIso) return isoFromParts(Number(typedIso[1]), Number(typedIso[2]), Number(typedIso[3]));

  return null;
}

export function formatDisplayDate(value: string): string {
  if (!isIsoDate(value)) return value;
  return `${value.slice(8, 10)}-${value.slice(5, 7)}-${value.slice(0, 4)}`;
}

export function toCanonicalDate(text: string): string {
  const trimmed = text.trim();
  if (trimmed === "") return "";
  return parseDisplayDate(trimmed) ?? trimmed;
}

export function isoDateToLocalDate(isoDate: string): Date | undefined {
  if (!isIsoDate(isoDate)) return undefined;
  const date = new Date(0);
  date.setFullYear(yearOf(isoDate), Number(isoDate.slice(5, 7)) - 1, Number(isoDate.slice(8, 10)));
  date.setHours(0, 0, 0, 0);
  return date;
}

export function localDateToIsoDate(date: Date): string {
  const isoDate = Number.isNaN(date.getTime())
    ? null
    : isoFromParts(date.getFullYear(), date.getMonth() + 1, date.getDate());
  if (isoDate === null)
    throw new RangeError("The date is not a calendar date from 0001-01-01 to 9999-12-31.");
  return isoDate;
}

export function compareIsoDates(left: string, right: string): number {
  if (left === right) return 0;
  return left < right ? -1 : 1;
}

export function clampIsoDate(isoDate: string, min?: string, max?: string): string {
  if (min !== undefined && isIsoDate(min) && compareIsoDates(isoDate, min) < 0) return min;
  if (max !== undefined && isIsoDate(max) && compareIsoDates(isoDate, max) > 0) return max;
  return isoDate;
}

export function calendarNavigationBounds(
  today: string,
  { min, max, selected }: CalendarNavigationOptions = {},
): CalendarNavigationBounds {
  const earliest = validIsoDate(min);
  const latest = validIsoDate(max);
  const chosen = yearOf(validIsoDate(selected) ?? today);
  const startYears = [yearOf(today) - calendarYearsBack, chosen];
  const endYears = [yearOf(today) + calendarYearsAhead, chosen];
  if (latest !== undefined) startYears.push(yearOf(latest));
  if (earliest !== undefined) endYears.push(yearOf(earliest));
  return {
    start: earliest ?? `${paddedYear(Math.min(...startYears))}-01-01`,
    end: latest ?? `${paddedYear(Math.max(...endYears))}-12-31`,
  };
}
