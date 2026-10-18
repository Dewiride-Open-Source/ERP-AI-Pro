import "server-only";

import { dateOnly } from "@dewiride/erp-api-client";

import { apiDateOf, type ApiDate } from "./date-only";

// A request body writes a date from its text, but a query string takes it only as the client's own DateOnly; this module
// stays apart from date-only.ts, which pages also bundle for the browser, so no page ships the client for it.
export function queryDateOf(isoDate: string): ApiDate {
  const { year, month, day } = apiDateOf(isoDate);
  return dateOnly(year, month, day);
}
