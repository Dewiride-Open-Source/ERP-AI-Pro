import assert from "node:assert/strict";
import { test } from "node:test";

import {
  countryOptions,
  type CurrencyItem,
  currencyOptions,
  displayDate,
  financialPeriodOptions,
  financialYearOptions,
  type FinancialYearItem,
  indianStateOptions,
  type IndianStateItem,
  listMessage,
  type ListRead,
  matchReferenceOptions,
} from "./reference-options.ts";

const today = "2026-11-07";

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
    { gstCode: "38", name: "Ladakh", kind: "unionTerritory", validFrom: "2020-01-01", validUntil: null },
  ],
};

const announcedChanges: ListRead<IndianStateItem> = {
  status: "read",
  items: [
    { gstCode: "27", name: "Maharashtra", kind: "state", validFrom: "2017-07-01", validUntil: null },
    {
      gstCode: "91",
      name: "Example territory",
      kind: "unionTerritory",
      validFrom: "2017-07-01",
      validUntil: "2027-01-25",
    },
    {
      gstCode: "92",
      name: "Example merged territory",
      kind: "unionTerritory",
      validFrom: "2027-01-26",
      validUntil: null,
    },
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

const countries = [
  { value: "AR", label: "Argentina (AR)" },
  { value: "IN", label: "India (IN)" },
  { value: "IO", label: "British Indian Ocean Territory (IO)" },
  { value: "KP", label: "Korea (Democratic People's Republic of) (KP)" },
  { value: "KR", label: "Korea, Republic of (KR)" },
  { value: "US", label: "United States of America (US)" },
] as const;

function labels(options: readonly { readonly label: string }[]): string[] {
  return options.map((option) => option.label);
}

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

test("matchReferenceOptions_CodeTypedInFull_PutsItsEntryFirstAndKeepsTheOthersInTheirOrder", () => {
  assert.deepEqual(labels(matchReferenceOptions(countries, "IN")), [
    "India (IN)",
    "Argentina (AR)",
    "British Indian Ocean Territory (IO)",
  ]);
  assert.deepEqual(labels(matchReferenceOptions(countries, "us")), ["United States of America (US)"]);
});

test("matchReferenceOptions_SeveralWords_FindsTheOptionsHoldingEveryWordWhateverTheirPunctuation", () => {
  assert.deepEqual(labels(matchReferenceOptions(countries, "korea republic")), [
    "Korea (Democratic People's Republic of) (KP)",
    "Korea, Republic of (KR)",
  ]);
  assert.deepEqual(labels(matchReferenceOptions(countries, "KR republic")), ["Korea, Republic of (KR)"]);
  assert.deepEqual(matchReferenceOptions(countries, "atlantis"), []);
});

test("matchReferenceOptions_NothingTyped_OffersEveryOptionInItsOrder", () => {
  assert.equal(matchReferenceOptions(countries, ""), countries);
  assert.equal(matchReferenceOptions(countries, "   "), countries);
});

test("matchReferenceOptions_StateCodeOrName_FindsTheStateWithoutCaseOrAccents", () => {
  const options = indianStateOptions(states, null, today, null);

  assert.deepEqual(labels(matchReferenceOptions(options, "27")), ["27 Maharashtra"]);
  assert.deepEqual(labels(matchReferenceOptions(options, "LADAKH")), ["38 Ladakh"]);
  assert.deepEqual(labels(matchReferenceOptions(options, "daman 26")), [
    "26 Dadra and Nagar Haveli and Daman and Diu",
  ]);
});

test("indianStateOptions_WithoutADate_OffersTheCodesInForceToday", () => {
  assert.deepEqual(labels(indianStateOptions(states, null, today, null)), [
    "26 Dadra and Nagar Haveli and Daman and Diu",
    "27 Maharashtra",
    "38 Ladakh",
  ]);
});

test("indianStateOptions_WithoutADateWhileChangesAreAnnounced_OffersTodaysCodesNotTheOnesWithoutAnEnd", () => {
  assert.deepEqual(labels(indianStateOptions(announcedChanges, null, today, null)), [
    "27 Maharashtra",
    "91 Example territory",
  ]);
  assert.deepEqual(labels(indianStateOptions(announcedChanges, null, "2027-01-25", null)), [
    "27 Maharashtra",
    "91 Example territory",
  ]);
  assert.deepEqual(labels(indianStateOptions(announcedChanges, null, "2027-01-26", null)), [
    "27 Maharashtra",
    "92 Example merged territory",
  ]);
});

test("indianStateOptions_OnADate_OffersTheCodesInForceThatDayWhateverToday", () => {
  assert.deepEqual(labels(indianStateOptions(states, "2020-01-25", today, null)), [
    "25 Daman and Diu",
    "26 Dadra and Nagar Haveli",
    "27 Maharashtra",
    "38 Ladakh",
  ]);
  assert.deepEqual(
    indianStateOptions(states, "2020-08-01", today, null).map((option) => option.value),
    ["26", "27", "38"],
  );
});

test("indianStateOptions_OnARowsFirstDay_OffersItAndNotTheDayBefore", () => {
  assert.deepEqual(labels(indianStateOptions(states, "2020-01-26", today, null)), [
    "25 Daman and Diu",
    "26 Dadra and Nagar Haveli and Daman and Diu",
    "27 Maharashtra",
    "38 Ladakh",
  ]);
  assert.ok(labels(indianStateOptions(states, "2020-01-01", today, null)).includes("38 Ladakh"));
  assert.ok(!labels(indianStateOptions(states, "2019-12-31", today, null)).includes("38 Ladakh"));
});

test("indianStateOptions_OnARowsLastDay_OffersItAndNotTheDayAfter", () => {
  assert.ok(labels(indianStateOptions(states, "2020-07-31", today, null)).includes("25 Daman and Diu"));
  assert.ok(!labels(indianStateOptions(states, "2020-08-01", today, null)).includes("25 Daman and Diu"));
  assert.ok(
    labels(indianStateOptions(states, "2020-01-25", today, null)).includes("26 Dadra and Nagar Haveli"),
  );
});

test("indianStateOptions_ChosenCodeOutOfForceOnTheDate_StaysChosenUnderItsLatestNameAndSaysSo", () => {
  assert.deepEqual(indianStateOptions(states, "2020-08-01", today, "25")[0], {
    value: "25",
    label: "25 Daman and Diu (not in force on 01-08-2020)",
  });
  assert.deepEqual(indianStateOptions(states, null, today, "25")[0], {
    value: "25",
    label: "25 Daman and Diu (not in force on 07-11-2026)",
  });
  assert.deepEqual(indianStateOptions(states, "2017-06-30", today, "26")[0], {
    value: "26",
    label: "26 Dadra and Nagar Haveli and Daman and Diu (not in force on 30-06-2017)",
  });
});

test("indianStateOptions_ChosenCodeNoEntryNames_StaysChosenUnderItsCode", () => {
  assert.deepEqual(indianStateOptions(states, null, today, "99")[0], {
    value: "99",
    label: "99 (name not available)",
  });
  assert.deepEqual(indianStateOptions({ status: "failed" }, null, today, "27"), [
    { value: "27", label: "27 (name not available)" },
  ]);
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

  assert.deepEqual(labels(options), ["VED Bolívar Soberano", "VES Bolívar Soberano"]);
});

test("currencyOptions_ChosenCodeThatIsExcluded_StaysChosenUnderItsName", () => {
  const list: ListRead<CurrencyItem> = {
    status: "read",
    items: [
      { code: "INR", name: "Indian Rupee", minorUnits: 2 },
      { code: "USD", name: "US Dollar", minorUnits: 2 },
    ],
  };

  assert.deepEqual(currencyOptions(list, "INR", ["INR"]), [
    { value: "INR", label: "INR Indian Rupee" },
    { value: "USD", label: "USD US Dollar" },
  ]);
  assert.deepEqual(currencyOptions(list, "XYZ", ["INR"])[0], {
    value: "XYZ",
    label: "XYZ (name not available)",
  });
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

test("financialPeriodOptions_ChosenPeriodTheYearLacks_StaysChosenUnderItsMonthAndYear", () => {
  assert.deepEqual(financialPeriodOptions({ status: "read", items: [year] }, "2026-27", "2019-04-01")[0], {
    value: "2019-04-01",
    label: "April 2019",
  });
  assert.deepEqual(financialPeriodOptions({ status: "failed" }, null, "2020-12-01"), [
    { value: "2020-12-01", label: "December 2020" },
  ]);
});

test("financialPeriodOptions_ChosenValueThatIsNoPeriodsFirstDay_StaysChosenUnderItself", () => {
  assert.deepEqual(financialPeriodOptions({ status: "failed" }, null, "2020-12-15"), [
    { value: "2020-12-15", label: "2020-12-15 (name not available)" },
  ]);
  assert.deepEqual(financialPeriodOptions({ status: "failed" }, null, "April"), [
    { value: "April", label: "April (name not available)" },
  ]);
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
