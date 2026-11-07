import assert from "node:assert/strict";
import { test } from "node:test";

import {
  countryOptions,
  currencyOptions,
  displayDate,
  financialPeriodOptions,
  financialYearOptions,
  type FinancialYearItem,
  indianStateOptions,
  type IndianStateItem,
  listMessage,
  type ListRead,
} from "./reference-options.ts";

const states: ListRead<IndianStateItem> = {
  status: "read",
  items: [
    {
      gstCode: "25",
      name: "Daman and Diu",
      kind: "unionTerritory",
      validFrom: "2017-07-01",
      validUntil: "2020-07-31",
    },
    {
      gstCode: "26",
      name: "Dadra and Nagar Haveli",
      kind: "unionTerritory",
      validFrom: "2017-07-01",
      validUntil: "2020-01-25",
    },
    {
      gstCode: "26",
      name: "Dadra and Nagar Haveli and Daman and Diu",
      kind: "unionTerritory",
      validFrom: "2020-01-26",
      validUntil: null,
    },
    { gstCode: "27", name: "Maharashtra", kind: "state", validFrom: "2017-07-01", validUntil: null },
  ],
};

const year: FinancialYearItem = {
  label: "2026-27",
  start: "2026-04-01",
  end: "2027-03-31",
  quarters: [{ number: 1, label: "Q1 2026-27", start: "2026-04-01", end: "2026-06-30" }],
  periods: [
    { number: 1, label: "April 2026", quarter: 1, start: "2026-04-01", end: "2026-04-30" },
    { number: 12, label: "March 2027", quarter: 4, start: "2027-03-01", end: "2027-03-31" },
  ],
};

test("countryOptions_ReadList_LabelsEachCountryWithItsCodeSoTypingTheCodeFindsIt", () => {
  const options = countryOptions({ status: "read", items: [{ code: "IN", name: "India" }] }, null);

  assert.deepEqual(options, [{ value: "IN", label: "India (IN)" }]);
});

test("countryOptions_ChosenCodeTheListLacks_KeepsItFirstUnderItsCode", () => {
  const options = countryOptions({ status: "read", items: [{ code: "IN", name: "India" }] }, "AN");

  assert.deepEqual(options[0], { value: "AN", label: "AN (name not available)" });
  assert.equal(options.length, 2);
});

test("countryOptions_ListThatCouldNotBeRead_KeepsOnlyTheChosenCode", () => {
  assert.deepEqual(countryOptions({ status: "failed" }, "IN"), [
    { value: "IN", label: "IN (name not available)" },
  ]);
  assert.deepEqual(countryOptions({ status: "disabled" }, null), []);
});

test("indianStateOptions_WithoutADate_OffersTheCodesInForceToday", () => {
  const options = indianStateOptions(states, null, null);

  assert.deepEqual(
    options.map((option) => option.label),
    ["26 Dadra and Nagar Haveli and Daman and Diu", "27 Maharashtra"],
  );
});

test("indianStateOptions_OnADate_OffersTheCodesInForceThatDay", () => {
  assert.deepEqual(
    indianStateOptions(states, "2020-01-25", null).map((option) => option.label),
    ["25 Daman and Diu", "26 Dadra and Nagar Haveli", "27 Maharashtra"],
  );
  assert.deepEqual(
    indianStateOptions(states, "2020-08-01", null).map((option) => option.value),
    ["26", "27"],
  );
});

test("indianStateOptions_ChosenCodeOutOfForceOnTheDate_StaysChosen", () => {
  const options = indianStateOptions(states, "2026-11-07", "25");

  assert.deepEqual(options[0], { value: "25", label: "25 (name not available)" });
});

test("currencyOptions_ExcludedCode_IsLeftOutAndEachLabelStartsWithTheCode", () => {
  const options = currencyOptions(
    {
      status: "read",
      items: [
        { code: "INR", name: "Indian Rupee", minorUnits: 2 },
        { code: "VED", name: "Bolívar Soberano", minorUnits: 2 },
        { code: "VES", name: "Bolívar Soberano", minorUnits: 2 },
      ],
    },
    null,
    ["INR"],
  );

  assert.deepEqual(
    options.map((option) => option.label),
    ["VED Bolívar Soberano", "VES Bolívar Soberano"],
  );
});

test("financialYearOptions_ReadYears_DescribeEachYearByItsDays", () => {
  assert.deepEqual(financialYearOptions({ status: "read", items: [year] }, null), [
    { value: "2026-27", label: "2026-27", description: "01-04-2026 to 31-03-2027" },
  ]);
});

test("financialPeriodOptions_YearOfTheList_OffersItsPeriodsByTheirFirstDay", () => {
  assert.deepEqual(financialPeriodOptions({ status: "read", items: [year] }, "2026-27", null), [
    { value: "2026-04-01", label: "April 2026", description: "Period 1, quarter 1 of 2026-27" },
    { value: "2027-03-01", label: "March 2027", description: "Period 12, quarter 4 of 2026-27" },
  ]);
  assert.deepEqual(financialPeriodOptions({ status: "read", items: [year] }, "2027-28", null), []);
});

test("listMessage_StatusOfTheList_SaysWhyNothingIsOffered", () => {
  assert.equal(
    listMessage({ status: "read", items: [] }, "countries", "No country matches."),
    "No country matches.",
  );
  assert.equal(
    listMessage({ status: "failed" }, "countries", "No country matches."),
    "The list of countries could not be read. Try again later.",
  );
  assert.equal(
    listMessage({ status: "disabled" }, "countries", "No country matches."),
    "The reference lists are switched off.",
  );
});

test("displayDate_IsoDate_IsWrittenDayMonthYear", () => {
  assert.equal(displayDate("2026-04-01"), "01-04-2026");
});
