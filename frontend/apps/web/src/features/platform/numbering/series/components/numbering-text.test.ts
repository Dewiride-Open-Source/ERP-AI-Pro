import assert from "node:assert/strict";
import { test } from "node:test";

import type { NumberFormat } from "./number-format.ts";
import type { NumberedDocument, NumberingYear, NumberSeries } from "./numbering-items.ts";
import {
  appliesFrom,
  dialogDescription,
  fixedNextNumberDescription,
  followingYear,
  incompletePreview,
  noRuleText,
  numbersGiven,
  pendingFormatText,
  prefixDescription,
  previewStatus,
  previewText,
  yearLines,
  yearText,
  type TextSegment,
} from "./numbering-text.ts";

function plainText(segments: readonly TextSegment[]): string {
  return segments.map((segment) => segment.text).join("");
}

const abbreviated: NumberFormat = { prefix: "TI.", year: "abbreviated", separator: ".", digits: 3 };

const plain: NumberFormat = { prefix: "TI", year: "none", separator: "", digits: 1 };

function year(values: Partial<NumberingYear> = {}): NumberingYear {
  return {
    year: "2026-27",
    format: abbreviated,
    maxLength: 14,
    inUse: true,
    firstText: "TI.26-27.001",
    nextText: "TI.26-27.043",
    lastText: "TI.26-27.99999",
    lastIssuedText: "TI.26-27.042",
    ...values,
  };
}

function numbered(
  series: Partial<NumberSeries> | null,
  values: Partial<NumberedDocument> = {},
): NumberedDocument {
  return {
    documentType: "test.numbering.invoices-01",
    title: "Test invoices 01",
    description: "Invoices of a test.",
    currentYear: "2026-27",
    rule: { maxLength: 14, specialCharacters: "-." },
    series:
      series === null
        ? null
        : {
            id: "0192f3c4-5b6a-7d8e-9f01-23456789abcd",
            format: abbreviated,
            version: "AAAA",
            years: [],
            ...series,
          },
    ...values,
  };
}

test("yearText_AYearThatHasGivenNumbers_CountsThemAndNamesTheNext", () => {
  assert.equal(
    plainText(yearText(year())),
    "42 numbers given, TI.26-27.001 to TI.26-27.042; next TI.26-27.043.",
  );
});

test("yearText_AYearThatHasGivenNumbers_MarksTheNumbersApartFromTheWords", () => {
  const numbers = (segments: readonly TextSegment[]) =>
    segments.filter((segment) => segment.isNumber).map((segment) => segment.text);

  assert.deepEqual(numbers(yearText(year())), ["TI.26-27.001", "TI.26-27.042", "TI.26-27.043"]);
});

test("yearText_AYearThatHasGivenOneNumber_NamesItOnce", () => {
  assert.equal(
    plainText(yearText(year({ nextText: "TI.26-27.002", lastIssuedText: "TI.26-27.001" }))),
    "1 number given, TI.26-27.001; next TI.26-27.002.",
  );
});

test("yearText_AYearThatStartedAfterNumbersGivenElsewhere_CountsFromItsFirstNumber", () => {
  assert.equal(
    plainText(
      yearText(year({ firstText: "TI.26-27.101", lastIssuedText: "TI.26-27.250", nextText: "TI.26-27.251" })),
    ),
    "150 numbers given, TI.26-27.101 to TI.26-27.250; next TI.26-27.251.",
  );
});

test("yearText_AYearThatHasGivenNoNumber_NamesItsFirst", () => {
  assert.equal(
    plainText(yearText(year({ inUse: false, nextText: "TI.26-27.001", lastIssuedText: null }))),
    "No number given yet; the first will be TI.26-27.001.",
  );
});

test("yearText_AYearThatUsedEveryNumberThatFits_SaysSo", () => {
  assert.equal(
    plainText(
      yearText(
        year({ format: plain, firstText: "TI1", nextText: null, lastText: "TI9", lastIssuedText: "TI9" }),
      ),
    ),
    "Every number that fits is used: 9 numbers, TI1 to TI9.",
  );
});

test("yearText_ManyNumbers_GroupsTheCountTheIndianWay", () => {
  assert.equal(
    plainText(
      yearText(
        year({
          format: plain,
          firstText: "TI1",
          nextText: "TI123457",
          lastText: "TI999999999999",
          lastIssuedText: "TI123456",
        }),
      ),
    ),
    "1,23,456 numbers given, TI1 to TI123456; next TI123457.",
  );
});

test("numbersGiven_RunningNumbersBeyondWhatAJsonNumberKeeps_CountsExactly", () => {
  const huge: NumberFormat = { prefix: "", year: "none", separator: "", digits: 1 };
  assert.equal(
    numbersGiven(
      year({
        format: huge,
        firstText: "9007199254740990",
        lastIssuedText: "9007199254740995",
        nextText: "9007199254740996",
        lastText: "999999999999999999",
      }),
    ),
    6n,
  );
  assert.equal(numbersGiven(year({ inUse: false, lastIssuedText: null })), 0n);
});

test("yearLines_ANumberingWhoseCurrentYearHasNotOpened_ShowsTheFirstNumberItWillGive", () => {
  const document = numbered({
    format: plain,
    years: [
      year({
        year: "2025-26",
        firstText: "TI.25-26.001",
        lastIssuedText: "TI.25-26.007",
        nextText: "TI.25-26.008",
      }),
    ],
  });

  assert.deepEqual(
    yearLines(document).map((line) => ({ year: line.year, text: plainText(line.text) })),
    [
      { year: "2026-27", text: "No number given yet; the first will be TI1." },
      { year: "2025-26", text: "7 numbers given, TI.25-26.001 to TI.25-26.007; next TI.25-26.008." },
    ],
  );
});

test("yearLines_ACurrentYearWithoutARule_IsNotOffered", () => {
  assert.deepEqual(yearLines(numbered({ years: [] }, { rule: null })), []);
});

test("yearLines_YearsInAnyOrder_ListsTheLatestFirst", () => {
  const document = numbered({
    years: [
      year({ year: "2024-25", inUse: false, lastIssuedText: null, nextText: "TI.24-25.001" }),
      year(),
      year({ year: "2025-26", inUse: false, lastIssuedText: null, nextText: "TI.25-26.001" }),
    ],
  });

  assert.deepEqual(
    yearLines(document).map((line) => line.year),
    ["2026-27", "2025-26", "2024-25"],
  );
});

test("yearLines_NoNumbering_HasNoYears", () => {
  assert.deepEqual(yearLines(numbered(null)), []);
});

test("pendingFormatText_AChangeAfterTheCurrentYearGaveANumber_NamesTheNextYearsFirstNumber", () => {
  const document = numbered({ format: { ...abbreviated, prefix: "TN.", digits: 4 }, years: [year()] });

  assert.equal(
    plainText(pendingFormatText(document) ?? []),
    "The new format applies from 2027-28, whose first number will be TN.27-28.0001.",
  );
});

test("pendingFormatText_NothingWaitsForTheNextYear_IsAbsent", () => {
  const changed = { ...abbreviated, prefix: "TN." };
  const cases: readonly NumberedDocument[] = [
    numbered(null),
    numbered({ years: [year()] }),
    numbered({ format: changed, years: [] }),
    numbered({ format: changed, years: [year({ inUse: false, lastIssuedText: null })] }),
    numbered({
      format: changed,
      years: [year(), year({ year: "2027-28", inUse: false, lastIssuedText: null, format: changed })],
    }),
  ];

  for (const document of cases) assert.equal(pendingFormatText(document), undefined);
});

test("followingYear_AFinancialYear_IsTheOneAfterIt", () => {
  assert.equal(followingYear("2026-27"), "2027-28");
  assert.equal(followingYear("2098-99"), "2099-00");
  assert.equal(followingYear("2099-00"), "2100-01");
});

test("noRuleText_WithAndWithoutANumbering_SaysWhatCannotHappen", () => {
  assert.equal(
    noRuleText({ currentYear: "2026-27", series: null }),
    "The ERP holds no rule for these numbers in 2026-27, so they cannot be numbered yet.",
  );
  assert.equal(
    noRuleText(numbered({ years: [] })),
    "The ERP holds no rule for these numbers in 2026-27, so their numbering cannot change.",
  );
});

test("previewText_AFormatThatFits_ShowsWhereTheYearStartsAndTheLastNumberThatFits", () => {
  const rule = { maxLength: 14, specialCharacters: "-." };

  assert.equal(
    plainText(previewText(abbreviated, "", rule, "2026-27")),
    "2026-27 starts at TI.26-27.001; the last number that fits in 14 characters is TI.26-27.99999.",
  );
  assert.equal(
    plainText(previewText(abbreviated, " 101 ", rule, "2027-28")),
    "2027-28 starts at TI.27-28.101; the last number that fits in 14 characters is TI.27-28.99999.",
  );
  assert.equal(
    plainText(previewText(abbreviated, "100000", rule, "2026-27")),
    "2026-27 starts at TI.26-27.001; the last number that fits in 14 characters is TI.26-27.99999.",
  );
});

test("previewText_AFormatTooLongOrNotYetComplete_SaysWhy", () => {
  const rule = { maxLength: 14, specialCharacters: "-." };

  assert.equal(
    plainText(previewText({ ...abbreviated, prefix: "INVOICES." }, "1", rule, "2026-27")),
    "INVOICES.26-27.001 has 18 characters, more than the 14 a number may have.",
  );
  assert.equal(plainText(previewText(undefined, "1", rule, "2026-27")), incompletePreview);
});

test("dialogDescription_EachStateOfTheNumbering_SaysWhenTheChangeApplies", () => {
  assert.equal(
    dialogDescription(numbered(null), false),
    "Its numbers start in 2026-27 at the next number you give, and every later financial year starts again at 1.",
  );
  assert.equal(
    dialogDescription(numbered({ years: [] }), false),
    "2026-27 has given no number yet, so a change applies to it at once.",
  );
  assert.equal(
    dialogDescription(numbered({ years: [year()] }), true),
    "2026-27 has given numbers, so they keep their format, and a change applies from 2027-28.",
  );
  assert.equal(appliesFrom("2026-27", false), "2026-27");
  assert.equal(appliesFrom("2026-27", true), "2027-28");
});

test("prefixDescription_TheRulesCharacters_AreListedWithLettersAndDigits", () => {
  assert.equal(
    prefixDescription({ maxLength: 14, specialCharacters: "-." }),
    "Optional. Capital letters, digits, - (hyphen) and . (full stop); the whole number may have at most 14 characters.",
  );
  assert.equal(
    prefixDescription({ maxLength: 12, specialCharacters: "" }),
    "Optional. Capital letters and digits; the whole number may have at most 12 characters.",
  );
});

test("fixedNextNumberDescription_AYearThatGaveNumbers_NamesItsNextOrSaysItIsUsedUp", () => {
  assert.equal(
    fixedNextNumberDescription("2026-27", "TI.26-27.043"),
    "2026-27 has given numbers, so it goes on from TI.26-27.043.",
  );
  assert.equal(
    fixedNextNumberDescription("2026-27", null),
    "2026-27 has used every number that fits, so its next number cannot change.",
  );
});

test("previewStatus_EachKindOfProblem_HoldsOneSentenceWhateverIsTyped", () => {
  const rule = { maxLength: 14, specialCharacters: "-." };
  const tooLong = "The numbers would have more than the 14 characters a number may have.";

  assert.equal(previewStatus(abbreviated, rule, "2026-27"), "");
  assert.equal(previewStatus({ ...abbreviated, prefix: "INVOICES." }, rule, "2026-27"), tooLong);
  assert.equal(previewStatus({ ...abbreviated, prefix: "INVOICES.TO." }, rule, "2026-27"), tooLong);
  assert.equal(previewStatus(undefined, rule, "2026-27"), incompletePreview);
});
