import type { ExchangeRateResponse } from "@dewiride/erp-api-client";
import assert from "node:assert/strict";
import { test } from "node:test";

import { apiDateOf } from "@/shared/api/dates/date-only";

import {
  correctedMessage,
  currencyFilterOptions,
  exchangeRateListRow,
  exchangeRateName,
  exchangeRateRecord,
  knownCurrencies,
  rateForUnitText,
  rateInputValue,
  rateText,
  recordedMessage,
  removal,
  sourceText,
  todayInIndia,
  unitText,
} from "./exchange-rate-display.ts";

const id = "0199a3f2-7c1e-7b4a-9d2e-000000000001";

const answered: ExchangeRateResponse = {
  id,
  currency: "JPY",
  rateDate: apiDateOf("2026-11-07"),
  rate: 56.1234,
  unit: 100,
  source: "FBIL reference rate, 1:30 PM",
  version: "AAAAAAAAB9E=",
};

const currencies = {
  status: "read",
  items: [
    { code: "CHF", name: "Swiss Franc", minorUnits: 2 },
    { code: "INR", name: "Indian Rupee", minorUnits: 2 },
    { code: "USD", name: "US Dollar", minorUnits: 2 },
  ],
} as const;

test("exchangeRateRecord_AnsweredRate_KeepsItsValuesWithTheDayAsText", () => {
  assert.deepEqual(exchangeRateRecord(answered), {
    id,
    currency: "JPY",
    rateDate: "2026-11-07",
    rate: 56.1234,
    unit: 100,
    source: "FBIL reference rate, 1:30 PM",
    version: "AAAAAAAAB9E=",
  });
});

test("exchangeRateRecord_BlankOrMissingSource_HasNoSource", () => {
  assert.equal(exchangeRateRecord({ ...answered, source: "" })?.source, null);
  assert.equal(exchangeRateRecord({ ...answered, source: null })?.source, null);
});

test("exchangeRateRecord_AnswerWithoutAValueItNeeds_IsNoRecord", () => {
  for (const missing of ["id", "currency", "rateDate", "rate", "unit", "version"] as const) {
    assert.equal(exchangeRateRecord({ ...answered, [missing]: null }), undefined, missing);
  }
});

test("exchangeRateListRow_AnsweredRate_IsTheTextsTheListShows", () => {
  assert.deepEqual(exchangeRateListRow(answered), {
    id,
    name: "JPY on 07-11-2026",
    day: "07-11-2026",
    currency: "JPY",
    rate: "₹56.1234",
    unit: "100",
    source: "FBIL reference rate, 1:30 PM",
  });
  assert.equal(exchangeRateListRow({ ...answered, source: null })?.source, "Not given");
  assert.equal(exchangeRateListRow({ ...answered, id: null }), undefined);
});

test("rateText_RecordedRate_ShowsRupeesWithFourDecimalsInIndianGroups", () => {
  assert.equal(rateText(88.69), "₹88.6900");
  assert.equal(rateText(0.0001), "₹0.0001");
  assert.equal(rateText(1_000_000), "₹10,00,000.0000");
  assert.equal(rateText(83.1235), "₹83.1235");
});

test("rateInputValue_RecordedRate_IsTheCanonicalTextAFormStartsFrom", () => {
  assert.equal(rateInputValue(88.69), "88.6900");
  assert.equal(rateInputValue(100), "100.0000");
});

test("unitText_Unit_IsGroupedTheIndianWay", () => {
  assert.deepEqual([1, 10, 100, 1_000, 10_000, 100_000].map(unitText), [
    "1",
    "10",
    "100",
    "1,000",
    "10,000",
    "1,00,000",
  ]);
});

test("rateForUnitText_RateUnitAndCurrency_ReadsAsTheRupeesForThoseUnits", () => {
  assert.equal(rateForUnitText(56.1234, 100, "JPY"), "₹56.1234 for 100 JPY");
});

test("sourceText_NoSource_SaysItWasNotGiven", () => {
  assert.equal(sourceText(null), "Not given");
  assert.equal(sourceText("RBI"), "RBI");
});

test("exchangeRateName_CurrencyAndDay_NamesTheRateAsTheAuditTrailDoes", () => {
  assert.equal(exchangeRateName("USD", "2026-03-31"), "USD on 31-03-2026");
});

test("recordedAndCorrectedMessages_CurrencyAndDay_NameTheRate", () => {
  assert.equal(recordedMessage("USD", "2026-03-31"), "Recorded the rate of USD for 31-03-2026.");
  assert.equal(correctedMessage("USD", "2026-03-31"), "Corrected the rate of USD on 31-03-2026.");
});

test("removal_CurrencyAndDay_AsksBeforeRemovingAndSaysTheDayIsFreeAgain", () => {
  assert.deepEqual(removal("USD", "2026-03-31"), {
    title: "Remove the rate of USD on 31-03-2026?",
    description:
      "The rate leaves the list, and its history stays in the audit trail. A rate of USD can then be recorded for 31-03-2026 again.",
    confirm: "Remove the rate",
    cancel: "Keep the rate",
    done: "Removed the rate of USD on 31-03-2026.",
    gone: "It had already been removed.",
  });
});

test("currencyFilterOptions_ReadList_OffersEveryForeignCurrencyByCodeAndName", () => {
  assert.deepEqual(currencyFilterOptions(currencies, []), [
    { value: "CHF", label: "CHF Swiss Franc" },
    { value: "USD", label: "USD US Dollar" },
  ]);
});

test("currencyFilterOptions_ListThatCouldNotBeRead_KeepsTheChosenCodesUnderTheirCodes", () => {
  assert.deepEqual(currencyFilterOptions({ status: "failed" }, ["SEK", "CHF"]), [
    { value: "SEK", label: "SEK" },
    { value: "CHF", label: "CHF" },
  ]);
});

test("knownCurrencies_ReadList_LeavesOutCodesItDoesNotHoldAndTheRupee", () => {
  assert.deepEqual(knownCurrencies(currencies, ["CHF", "INR", "XYZ", "USD"]), ["CHF", "USD"]);
  assert.deepEqual(knownCurrencies({ status: "failed" }, ["CHF", "XYZ"]), ["CHF", "XYZ"]);
});

test("todayInIndia_Now_IsACalendarDate", () => {
  assert.match(todayInIndia(), /^\d{4}-\d{2}-\d{2}$/);
});
