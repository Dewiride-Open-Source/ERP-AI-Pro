import type { ParameterAsOf, ParameterSetBasis } from "@dewiride/erp-api-client";

import type { VersionSummary } from "./set-items.ts";

export const parameterSetsDescription =
  "The government rates, codes, due dates and limits the ERP works with, each from the day it applies. Open a set to see its earlier and upcoming versions and what each one changed. They change only with an update of the ERP.";

export const parameterSetsUnavailable = "The statutory parameter sets could not be read. Try again later.";

export const parameterSetUnavailable = "This statutory parameter set could not be read. Try again later.";

export const nothingInForce = "Nothing is in force yet.";

const basisLabels: Readonly<Record<ParameterSetBasis, string>> = {
  law: "Government table",
  interpretation: "The ERP's reading",
};

const asOfTexts: Readonly<Record<ParameterAsOf, string>> = {
  supplyDate: "Each version applies to supplies made on or after the day it starts.",
  taxPeriodStart:
    "Each version applies to the tax periods that begin on or after the day it starts: a month, a quarter or a financial year.",
};

const longDates = new Intl.DateTimeFormat("en-IN", {
  day: "numeric",
  month: "long",
  year: "numeric",
  timeZone: "UTC",
});

const counts = new Intl.NumberFormat("en-IN");

export function basisLabel(basis: ParameterSetBasis): string {
  return basisLabels[basis];
}

export function asOfText(asOf: ParameterAsOf): string {
  return asOfTexts[asOf];
}

// A version's days are calendar days of India written as yyyy-MM-dd, so they are read at midnight UTC and written in UTC,
// which keeps the day whatever the time zone of the server.
export function longDate(isoDate: string): string {
  return longDates.format(new Date(`${isoDate}T00:00:00Z`));
}

export function fromText(isoDate: string): string {
  return `From ${longDate(isoDate)}`;
}

export function versionRange(version: { readonly from: string; readonly until: string | null }): string {
  return version.until === null
    ? fromText(version.from)
    : `${fromText(version.from)} to ${longDate(version.until)}`;
}

export function changeCounts(version: VersionSummary): string {
  return `${counts.format(version.addedCount)} added · ${counts.format(version.changedCount)} changed · ${counts.format(version.removedCount)} removed`;
}

export function entryCountText(entries: number): string {
  if (entries === 0) return "None";
  return entries === 1 ? "1 entry" : `${counts.format(entries)} entries`;
}
