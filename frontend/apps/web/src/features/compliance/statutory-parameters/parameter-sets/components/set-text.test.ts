import assert from "node:assert/strict";
import { test } from "node:test";

import {
  asOfText,
  basisLabel,
  changeCounts,
  entryCountText,
  fromText,
  longDate,
  longDayMonth,
  versionRange,
} from "./set-text.ts";

test("basisLabel_EachBasis_NamesWhetherTheGovernmentPrintsTheTable", () => {
  assert.equal(basisLabel("law"), "Government table");
  assert.equal(basisLabel("interpretation"), "The ERP's reading");
});

test("asOfText_EachMeaning_SaysWhatTheDayAVersionStartsGoverns", () => {
  assert.equal(
    asOfText("supplyDate"),
    "Each version applies to supplies made on or after the day it starts.",
  );
  assert.equal(
    asOfText("taxPeriodStart"),
    "Each version applies to the tax periods that begin on or after the day it starts: a month, a quarter or a financial year.",
  );
  assert.equal(
    asOfText("deductionDate"),
    "Each version applies to tax deducted on or after the day it starts: on the credit or the payment at which the Act requires the deduction.",
  );
  assert.equal(
    asOfText("financialYearStart"),
    "Each version applies to the financial years that begin on or after the day it starts.",
  );
  assert.equal(asOfText("wageDate"), "Each version applies to wages earned on or after the day it starts.");
  assert.equal(
    asOfText("wagePeriodEnd"),
    "Each version applies to the wage periods that end on or after the day it starts.",
  );
  assert.equal(
    asOfText("employmentEndDate"),
    "Each version applies to employment that ends on or after the day it starts.",
  );
  assert.equal(
    asOfText("accountingYearEnd"),
    "Each version applies to the accounting years that end on or after the day it starts.",
  );
  assert.equal(
    asOfText("contributionPeriodEnd"),
    "Each version applies to the contribution periods that end on or after the day it starts.",
  );
});

test("longDayMonth_DayAndMonth_IsTheDayAndMonthInWords", () => {
  assert.equal(longDayMonth("04-01"), "1 April");
  assert.equal(longDayMonth("09-30"), "30 September");
  assert.equal(longDayMonth("02-29"), "29 February");
});

test("longDate_IsoDate_IsTheDayMonthAndYearInWords", () => {
  assert.equal(longDate("2026-03-05"), "5 March 2026");
  assert.equal(longDate("2024-02-29"), "29 February 2024");
  assert.equal(longDate("2025-12-31"), "31 December 2025");
});

test("fromText_IsoDate_SaysTheDayItStarts", () => {
  assert.equal(fromText("2023-10-01"), "From 1 October 2023");
});

test("versionRange_OpenVersion_SaysOnlyWhenItStarts", () => {
  assert.equal(versionRange({ from: "2026-04-01", until: null }), "From 1 April 2026");
});

test("versionRange_VersionALaterOneEnds_SaysItsFirstAndLastDay", () => {
  assert.equal(
    versionRange({ from: "2023-04-01", until: "2026-03-31" }),
    "From 1 April 2023 to 31 March 2026",
  );
});

test("changeCounts_Version_CountsTheAddedChangedAndRemovedEntriesInIndianGrouping", () => {
  assert.equal(
    changeCounts({
      from: "2026-04-01",
      until: null,
      instruments: [],
      addedCount: 0,
      changedCount: 1,
      removedCount: 123456,
    }),
    "0 added · 1 changed · 1,23,456 removed",
  );
});

test("entryCountText_NoneOneOrMany_CountsTheEntries", () => {
  assert.equal(entryCountText(0), "None");
  assert.equal(entryCountText(1), "1 entry");
  assert.equal(entryCountText(12345), "12,345 entries");
});
