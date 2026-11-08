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
  deductionDate:
    "Each version applies to tax deducted on or after the day it starts: on the credit or the payment at which the Act requires the deduction.",
  financialYearStart: "Each version applies to the financial years that begin on or after the day it starts.",
  wageDate: "Each version applies to wages earned on or after the day it starts.",
  wagePeriodEnd: "Each version applies to the wage periods that end on or after the day it starts.",
  employmentEndDate: "Each version applies to employment that ends on or after the day it starts.",
  accountingYearEnd: "Each version applies to the accounting years that end on or after the day it starts.",
  contributionPeriodEnd:
    "Each version applies to the contribution periods that end on or after the day it starts.",
};

const longDates = new Intl.DateTimeFormat("en-IN", {
  day: "numeric",
  month: "long",
  year: "numeric",
  timeZone: "UTC",
});

const longDaysOfMonths = new Intl.DateTimeFormat("en-IN", {
  day: "numeric",
  month: "long",
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

// A day and month comes as MM-dd and names that day of every year, so it is written in the leap year 2000, which holds
// 29 February too.
export function longDayMonth(dayMonth: string): string {
  return longDaysOfMonths.format(new Date(`2000-${dayMonth}T00:00:00Z`));
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
