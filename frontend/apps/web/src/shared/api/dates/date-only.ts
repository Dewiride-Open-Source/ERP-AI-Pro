import type { PersonResponse } from "@dewiride/erp-api-client";

export type ApiDate = NonNullable<PersonResponse["dateOfJoining"]>;

const isoDatePattern = /^(\d{4})-(\d{2})-(\d{2})$/;

function padded(value: number, digits: number): string {
  return String(value).padStart(digits, "0");
}

export function isoDateOf(value: ApiDate | null | undefined): string | undefined {
  if (value === null || value === undefined) return undefined;
  return `${padded(value.year, 4)}-${padded(value.month, 2)}-${padded(value.day, 2)}`;
}

export function apiDateOf(isoDate: string): ApiDate {
  const match = isoDatePattern.exec(isoDate);
  if (match === null) throw new RangeError(`"${isoDate}" is not a date written as yyyy-MM-dd.`);
  return { year: Number(match[1]), month: Number(match[2]), day: Number(match[3]), toString: () => isoDate };
}
