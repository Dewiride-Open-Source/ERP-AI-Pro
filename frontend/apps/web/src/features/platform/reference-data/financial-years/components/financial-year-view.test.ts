import assert from "node:assert/strict";
import { test } from "node:test";

import {
  financialYearChoice,
  financialYearOf,
  financialYearsHref,
  readFinancialYearView,
  startYearOf,
} from "./financial-year-view.ts";

const path = "/platform/reference-data/financial-years";

const choice = financialYearChoice("2026-11-07");

test("financialYearOf_DayFromAprilOn_IsTheYearThatStartedThatApril", () => {
  assert.equal(financialYearOf("2026-04-01"), "2026-27");
  assert.equal(financialYearOf("2026-11-07"), "2026-27");
  assert.equal(financialYearOf("2026-12-31"), "2026-27");
});

test("financialYearOf_DayFromJanuaryToMarch_IsTheYearThatStartedTheAprilBefore", () => {
  assert.equal(financialYearOf("2026-03-31"), "2025-26");
  assert.equal(financialYearOf("2027-01-01"), "2026-27");
});

test("financialYearOf_YearEndingInANewCentury_WritesTheLastTwoDigitsOfTheNextYear", () => {
  assert.equal(financialYearOf("1999-05-01"), "1999-00");
  assert.equal(financialYearOf("2009-04-01"), "2009-10");
});

test("financialYearChoice_Today_OffersTheTenYearsBeforeTheCurrentOneAndTheOneAfter", () => {
  assert.deepEqual(choice, { current: "2026-27", from: "2016-17", to: "2027-28" });
  assert.deepEqual(financialYearChoice("2027-02-14"), { current: "2026-27", from: "2016-17", to: "2027-28" });
});

test("startYearOf_Label_ReadsTheCalendarYearItStartsIn", () => {
  assert.equal(startYearOf("2026-27"), 2026);
  assert.equal(startYearOf("1999-00"), 1999);
});

test("startYearOf_TextThatIsNoLabel_IsUndefined", () => {
  for (const text of ["2026-28", "26-27", "2026/27", "2026-2027", "0000-01", " 2026-27"]) {
    assert.equal(startYearOf(text), undefined, text);
  }
});

test("readFinancialYearView_NoParameters_IsTheCurrentYear", () => {
  assert.deepEqual(readFinancialYearView({}, choice), { view: { year: undefined }, canonical: true });
});

test("readFinancialYearView_OfferedYear_IsKeptAsTheCanonicalAddress", () => {
  for (const year of ["2016-17", "2025-26", "2027-28"]) {
    assert.deepEqual(readFinancialYearView({ year }, choice), { view: { year }, canonical: true }, year);
  }
});

test("readFinancialYearView_CurrentYearNamed_LeavesItOutOfTheCanonicalAddress", () => {
  assert.deepEqual(readFinancialYearView({ year: "2026-27" }, choice), {
    view: { year: undefined },
    canonical: false,
  });
});

test("readFinancialYearView_YearNotOfferedMalformedOrRepeated_DropsIt", () => {
  for (const year of ["2015-16", "2028-29", "2026-28", "2026", "", ["2025-26", "2024-25"]]) {
    assert.deepEqual(
      readFinancialYearView({ year }, choice),
      { view: { year: undefined }, canonical: false },
      JSON.stringify(year),
    );
  }
});

test("readFinancialYearView_ParameterThePageDoesNotKnow_IsNotCanonical", () => {
  assert.deepEqual(readFinancialYearView({ year: "2025-26", q: "april" }, choice), {
    view: { year: "2025-26" },
    canonical: false,
  });
});

test("financialYearsHref_View_NamesTheYearOnlyWhenItIsNotTheCurrentOne", () => {
  assert.equal(financialYearsHref({ year: undefined }), path);
  assert.equal(financialYearsHref({ year: "2025-26" }), `${path}?year=2025-26`);
});

test("financialYearsHref_CanonicalAddress_ReadsBackAsTheSameView", () => {
  const address = new URL(financialYearsHref({ year: "2019-20" }), "https://erp.example");

  assert.deepEqual(readFinancialYearView(Object.fromEntries(address.searchParams), choice), {
    view: { year: "2019-20" },
    canonical: true,
  });
});
