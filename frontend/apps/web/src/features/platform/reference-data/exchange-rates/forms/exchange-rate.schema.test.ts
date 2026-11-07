import assert from "node:assert/strict";
import { test } from "node:test";
import type { z } from "zod";

import {
  changedMeanwhileCode,
  correctExchangeRateProblemMessages,
  correctExchangeRateSchema,
  correctExchangeRateValues,
  importExchangeRatesSchema,
  maxImportedRates,
  recordExchangeRateProblemMessages,
  recordExchangeRateSchema,
  recordExchangeRateValues,
  removeExchangeRateProblemMessages,
  sourceMaxLength,
} from "./exchange-rate.schema.ts";

const today = "2026-11-07";

const id = "0199a3f2-7c1e-7b4a-9d2e-000000000001";

const recording = recordExchangeRateSchema(today);

const filled = {
  currency: "USD",
  rateDate: "2026-11-06",
  rate: "88.69",
  unit: "1",
  source: "FBIL reference rate",
};

function reported(schema: z.ZodType, values: unknown): string[] {
  const result = schema.safeParse(values);
  return result.success
    ? []
    : result.error.issues.map((issue) => `${issue.path.join(".")}: ${issue.message}`);
}

function firstMessages(values: unknown): Record<string, string> {
  const result = recording.safeParse(values);
  const messages: Record<string, string> = {};
  for (const issue of result.success ? [] : result.error.issues) {
    messages[issue.path.join(".")] ??= issue.message;
  }
  return messages;
}

test("recordExchangeRateSchema_StartingValues_AsksForTheCurrencyDayAndRateAndStartsAtOneUnit", () => {
  assert.equal(recordExchangeRateValues.unit, "1");
  assert.deepEqual(reported(recording, recordExchangeRateValues), [
    "currency: Choose the currency.",
    "rateDate: Enter the day the rate is for.",
    "rate: Enter the rate.",
  ]);
});

test("recordExchangeRateSchema_FilledValues_AreTheRequestsValues", () => {
  assert.deepEqual(recording.parse({ ...filled, rate: "1,234.5", source: "  FBIL  " }), {
    currency: "USD",
    rateDate: "2026-11-06",
    rate: "1234.5",
    unit: 1,
    source: "FBIL",
  });
});

test("recordExchangeRateSchema_BlankSource_IsAbsent", () => {
  assert.equal(recording.parse({ ...filled, source: "   " }).source, undefined);
});

test("recordExchangeRateSchema_TheRupeeOrACodeNotFromTheList_IsRefusedOnTheCurrency", () => {
  assert.deepEqual(firstMessages({ ...filled, currency: "INR" }), {
    currency: "Choose a foreign currency; the rupee has no rate against itself.",
  });
  assert.deepEqual(firstMessages({ ...filled, currency: "US" }), {
    currency: "Choose the currency from the list.",
  });
});

test("recordExchangeRateSchema_DayAfterToday_IsRefusedAndTodayIsAccepted", () => {
  assert.deepEqual(firstMessages({ ...filled, rateDate: "2026-11-08" }), {
    rateDate: "Enter a date on or before 07-11-2026.",
  });
  assert.deepEqual(firstMessages({ ...filled, rateDate: today }), {});
});

test("recordExchangeRateSchema_RateOfZeroOverTheCeilingOrTooPrecise_IsRefusedOnTheRate", () => {
  assert.deepEqual(firstMessages({ ...filled, rate: "0" }), { rate: "Enter a rate of more than ₹0." });
  assert.deepEqual(firstMessages({ ...filled, rate: "0.0000" }), { rate: "Enter a rate of more than ₹0." });
  assert.deepEqual(firstMessages({ ...filled, rate: "1000000.0001" }), {
    rate: "Enter an amount of at most ₹10,00,000.0000.",
  });
  assert.deepEqual(firstMessages({ ...filled, rate: "83.12345" }), {
    rate: "Enter at most 4 digits after the decimal point.",
  });
  assert.deepEqual(firstMessages({ ...filled, rate: "0.0001" }), {});
  assert.deepEqual(firstMessages({ ...filled, rate: "1000000" }), {});
});

test("recordExchangeRateSchema_UnitOutsideTheQuotedUnits_IsRefusedAndAGroupedUnitIsRead", () => {
  assert.deepEqual(firstMessages({ ...filled, unit: "5" }), {
    unit: "Choose how many units of the currency the rate is for.",
  });
  assert.equal(recording.parse({ ...filled, unit: "1,00,000" }).unit, 100_000);
});

test("recordExchangeRateSchema_SourceOverTheLimit_IsRefusedOnTheSource", () => {
  assert.deepEqual(firstMessages({ ...filled, source: "x".repeat(sourceMaxLength + 1) }), {
    source: `Use ${sourceMaxLength} characters or fewer.`,
  });
  assert.deepEqual(firstMessages({ ...filled, source: "x".repeat(sourceMaxLength) }), {});
});

test("correctExchangeRateValues_RecordedRate_StartsFromItsValuesAndVersion", () => {
  const values = correctExchangeRateValues({
    id,
    rate: "56.1234",
    unit: 100,
    source: null,
    version: "AAAAAAAAB9E=",
  });

  assert.deepEqual(values, { id, rate: "56.1234", unit: "100", source: "", version: "AAAAAAAAB9E=" });
  const parsed = correctExchangeRateSchema.parse(values);
  assert.equal(parsed.rate, "56.1234");
  assert.equal(parsed.unit, 100);
  assert.equal(parsed.source, undefined);
  assert.equal(parsed.version, "AAAAAAAAB9E=");
});

test("correctExchangeRateSchema_MissingIdOrVersion_SaysThePageIsOutOfDate", () => {
  assert.deepEqual(
    reported(correctExchangeRateSchema, { id: "", rate: "1", unit: "1", source: "", version: "" }),
    [
      "id: This page is out of date. Reload the page, then send the form again.",
      "version: This page is out of date. Reload the page, then send the form again.",
    ],
  );
});

test("correctExchangeRateProblemMessages_ChangedMeanwhile_OffersTheLatestRate", () => {
  assert.equal(
    correctExchangeRateProblemMessages[changedMeanwhileCode],
    "Someone changed this rate after the page was loaded. Show the latest rate, then correct it again.",
  );
});

test("recordExchangeRateProblemMessages_RecordedMeanwhile_SaysTheRateWasNotRecordedInPlainWords", () => {
  assert.equal(
    recordExchangeRateProblemMessages[changedMeanwhileCode],
    "Someone recorded a rate of this currency for that day while this one was being saved, so it was not recorded. Open the exchange rates to see that rate.",
  );
});

test("removeExchangeRateProblemMessages_ChangedMeanwhile_AsksToReloadInPlainWords", () => {
  assert.equal(
    removeExchangeRateProblemMessages[changedMeanwhileCode],
    "Someone changed this rate after the page was loaded, so it was not removed. Reload the page, then remove it again.",
  );
});

test("importExchangeRatesSchema_NoRatesOrTooMany_IsRefusedOnTheRates", () => {
  const importing = importExchangeRatesSchema(today);

  assert.deepEqual(reported(importing, { rates: [] }), [
    "rates: Choose a file that holds at least one rate.",
  ]);
  assert.deepEqual(
    reported(importing, { rates: Array.from({ length: maxImportedRates + 1 }, () => filled) }),
    ["rates: An import takes at most 1,000 rates; split the file."],
  );
});

test("importExchangeRatesSchema_ARowThatBreaksARule_IsReportedAtItsIndex", () => {
  assert.deepEqual(
    reported(importExchangeRatesSchema(today), { rates: [filled, { ...filled, rateDate: "2026-11-08" }] }),
    ["rates.1.rateDate: Enter a date on or before 07-11-2026."],
  );
});
