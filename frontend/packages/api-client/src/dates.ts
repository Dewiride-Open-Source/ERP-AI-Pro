import { DateOnly } from "@microsoft/kiota-abstractions";

// A request builder writes a date into the query string only when it is Kiota's own DateOnly, which it recognises by its
// class, so a caller outside this package builds one here.
export function dateOnly(year: number, month: number, day: number): DateOnly {
  return new DateOnly({ year, month, day });
}
