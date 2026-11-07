import assert from "node:assert/strict";
import { test } from "node:test";

import type { FinancialYearItem } from "../../reference-lists/components/reference-options.ts";

import { periodRows, quarterRows, yearDays } from "./financial-year-rows.ts";

const year: FinancialYearItem = {
  label: "2026-27",
  start: "2026-04-01",
  end: "2027-03-31",
  quarters: [
    { number: 1, label: "Q1 2026-27", start: "2026-04-01", end: "2026-06-30" },
    { number: 4, label: "Q4 2026-27", start: "2027-01-01", end: "2027-03-31" },
  ],
  periods: [
    { number: 1, label: "April 2026", quarter: 1, start: "2026-04-01", end: "2026-04-30" },
    { number: 12, label: "March 2027", quarter: 4, start: "2027-03-01", end: "2027-03-31" },
  ],
};

test("yearDays_Year_NamesItsFirstAndLastDayAsDayMonthYear", () => {
  assert.equal(yearDays(year), "From 01-04-2026 to 31-03-2027.");
});

test("quarterRows_Year_ShowsEachQuarterWithItsFirstAndLastDay", () => {
  assert.deepEqual(quarterRows(year), [
    { id: "2026-04-01", cells: { quarter: "Q1 2026-27", from: "01-04-2026", until: "30-06-2026" } },
    { id: "2027-01-01", cells: { quarter: "Q4 2026-27", from: "01-01-2027", until: "31-03-2027" } },
  ]);
});

test("periodRows_Year_NamesEachPeriodByItsMonthNumberAndQuarter", () => {
  assert.deepEqual(periodRows(year), [
    {
      id: "2026-04-01",
      cells: {
        month: "April 2026",
        period: "1",
        quarter: "Q1 2026-27",
        from: "01-04-2026",
        until: "30-04-2026",
      },
    },
    {
      id: "2027-03-01",
      cells: {
        month: "March 2027",
        period: "12",
        quarter: "Q4 2026-27",
        from: "01-03-2027",
        until: "31-03-2027",
      },
    },
  ]);
});

test("periodRows_QuarterTheYearDoesNotList_ShowsTheQuarterNumber", () => {
  const rows = periodRows({ ...year, quarters: [] });

  assert.deepEqual(
    rows.map((row) => row.cells.quarter),
    ["1", "4"],
  );
});
