import assert from "node:assert/strict";
import { test } from "node:test";

import { issuesToFieldErrors } from "@/shared/forms/errors/field-paths";

import type { NumberedDocument, NumberingYear } from "../components/numbering-items.ts";
import {
  changeNumberSeriesSchema,
  changeNumberSeriesValues,
  nextNumberFixed,
  nextNumberLimit,
  setUpNumberSeriesSchema,
  setUpNumberSeriesValues,
  type ChangeNumberSeriesValues,
  type NumberingRules,
  type SetUpNumberSeriesValues,
} from "./number-series.schema.ts";

// The rule is made up, as the API's own tests make it up, so that no statutory value is written here.
const rules: NumberingRules = { rule: { maxLength: 14, specialCharacters: "-." }, currentYear: "2026-27" };

const seriesId = "0192f3c4-5b6a-7d8e-9f01-23456789abcd";

function setUp(values: Partial<SetUpNumberSeriesValues> = {}): SetUpNumberSeriesValues {
  return {
    documentType: "test.numbering.invoices-01",
    format: { prefix: "TI.", year: "abbreviated", separator: ".", digits: "3" },
    nextNumber: "1",
    ...values,
  };
}

function change(values: Partial<ChangeNumberSeriesValues> = {}): ChangeNumberSeriesValues {
  return {
    id: seriesId,
    format: { prefix: "TI.", year: "abbreviated", separator: ".", digits: "3" },
    nextNumber: "43",
    version: "AAAAAAAAB9E=",
    ...values,
  };
}

function errorsOf(result: {
  success: boolean;
  error?: { issues: readonly { path: PropertyKey[]; message: string }[] };
}) {
  assert.equal(result.success, false);
  return issuesToFieldErrors(result.error?.issues ?? []);
}

function year(values: Partial<NumberingYear> = {}): NumberingYear {
  return {
    year: "2026-27",
    format: { prefix: "TI.", year: "abbreviated", separator: ".", digits: 3 },
    maxLength: 14,
    inUse: false,
    firstText: "TI.26-27.007",
    nextText: "TI.26-27.007",
    lastText: "TI.26-27.99999",
    lastIssuedText: null,
    ...values,
  };
}

function numbered(years: readonly NumberingYear[]): NumberedDocument {
  return {
    documentType: "test.numbering.invoices-01",
    title: "Test invoices 01",
    description: "Invoices of a test.",
    currentYear: "2026-27",
    rule: rules.rule,
    series: {
      id: seriesId,
      format: { prefix: "TN-", year: "full", separator: "-", digits: 2 },
      version: "AAAAAAAAB9E=",
      years,
    },
  };
}

test("setUpNumberSeriesSchema_AFormatTheRuleAllows_IsTheRequest", () => {
  const parsed = setUpNumberSeriesSchema(rules).safeParse(setUp({ nextNumber: " 101 " }));

  assert.equal(parsed.success, true);
  assert.deepEqual(parsed.data, {
    documentType: "test.numbering.invoices-01",
    format: { prefix: "TI.", year: "abbreviated", separator: ".", digits: 3 },
    nextNumber: 101,
  });
});

test("setUpNumberSeriesSchema_TheYearNotShown_SendsNoSeparator", () => {
  const parsed = setUpNumberSeriesSchema(rules).safeParse(
    setUp({ format: { prefix: "TI", year: "none", separator: ".", digits: "4" } }),
  );

  assert.equal(parsed.success, true);
  assert.deepEqual(parsed.data?.format, { prefix: "TI", year: "none", separator: "", digits: 4 });
});

test("setUpNumberSeriesSchema_EachFieldTheRuleRefuses_HoldsTheApisMessage", () => {
  const { fieldErrors, formErrors } = errorsOf(
    setUpNumberSeriesSchema(rules).safeParse(
      setUp({ format: { prefix: "ti/", year: "full", separator: "/", digits: "19" }, nextNumber: "" }),
    ),
  );

  assert.deepEqual(fieldErrors, {
    "format.prefix": ['Use at most 32 characters, each a capital letter, a digit or "-" or ".".'],
    "format.separator": ['Use "-" or ".", or nothing.'],
    "format.digits": ["Use 1 to 18 digits for the running number."],
    nextNumber: ["Enter the next number."],
  });
  assert.deepEqual(formErrors, []);
});

test("setUpNumberSeriesSchema_AFirstNumberTooLong_IsARuleAboutTheWholeNumber", () => {
  const { fieldErrors, formErrors } = errorsOf(
    setUpNumberSeriesSchema(rules).safeParse(
      setUp({ format: { prefix: "INVOICE.", year: "full", separator: ".", digits: "3" } }),
    ),
  );

  assert.deepEqual(fieldErrors, {});
  assert.deepEqual(formErrors, [
    "The first number, INVOICE.2026-27.001, has 19 characters; a number may have at most 14.",
  ]);
});

test("setUpNumberSeriesSchema_ANumberThatWouldBeginWithZero_NamesTheDigits", () => {
  const { fieldErrors } = errorsOf(
    setUpNumberSeriesSchema(rules).safeParse(
      setUp({ format: { prefix: "", year: "none", separator: "", digits: "3" } }),
    ),
  );

  assert.deepEqual(fieldErrors, {
    "format.digits": ["A number must begin with a letter or a digit other than 0, and 001 does not."],
  });
});

test("setUpNumberSeriesSchema_ANextNumberBeyondTheLastThatFits_GivesTheRange", () => {
  const schema = setUpNumberSeriesSchema(rules);

  assert.deepEqual(errorsOf(schema.safeParse(setUp({ nextNumber: "100000" }))).fieldErrors, {
    nextNumber: ["Use a number from 1 to 99999."],
  });
  assert.deepEqual(errorsOf(schema.safeParse(setUp({ nextNumber: "0" }))).fieldErrors, {
    nextNumber: ["Use a number from 1 to 99999."],
  });
  assert.deepEqual(errorsOf(schema.safeParse(setUp({ nextNumber: "1.5" }))).fieldErrors, {
    nextNumber: ["Enter a whole number, such as 1."],
  });
  assert.equal(schema.safeParse(setUp({ nextNumber: "99999" })).success, true);
});

test("setUpNumberSeriesSchema_WithoutTheRules_ChecksOnlyTheShapeOfTheRequest", () => {
  const schema = setUpNumberSeriesSchema();

  assert.equal(
    schema.safeParse(setUp({ format: { prefix: "ti", year: "full", separator: "/", digits: "3" } })).success,
    true,
  );
  assert.deepEqual(
    errorsOf(
      schema.safeParse(
        setUp({
          format: { prefix: "A".repeat(33), year: "monthly", separator: "//", digits: "x" },
          nextNumber: "9007199254740992",
        }),
      ),
    ).fieldErrors,
    {
      "format.prefix": ["Use at most 32 characters."],
      "format.year": ["Choose how the financial year is shown, or that it is not shown."],
      "format.separator": ["Use one character, or nothing."],
      "format.digits": ["Use 1 to 18 digits for the running number."],
      nextNumber: ["Use a number from 1 to 9007199254740991."],
    },
  );
  assert.deepEqual(errorsOf(schema.safeParse(setUp({ documentType: "" }))).fieldErrors, {
    documentType: ["This page is out of date. Reload the page, then send the form again."],
  });
});

test("setUpNumberSeriesSchema_ANextNumberBeyondWhatTheRequestKeeps_IsRefused", () => {
  const wide: NumberingRules = { rule: { maxLength: 40, specialCharacters: "-." }, currentYear: "2026-27" };
  const values = setUp({ format: { prefix: "TI", year: "none", separator: "", digits: "1" } });

  assert.equal(
    setUpNumberSeriesSchema(wide).safeParse({ ...values, nextNumber: "9007199254740991" }).success,
    true,
  );
  assert.deepEqual(
    errorsOf(setUpNumberSeriesSchema(wide).safeParse({ ...values, nextNumber: "9007199254740992" }))
      .fieldErrors,
    { nextNumber: ["Use a number from 1 to 9007199254740991."] },
  );
});

test("changeNumberSeriesSchema_AYearThatTakesANextNumber_SendsIt", () => {
  const parsed = changeNumberSeriesSchema({ ...rules, nextNumberFixed: false }).safeParse(change());

  assert.equal(parsed.success, true);
  assert.deepEqual(parsed.data, {
    id: seriesId,
    format: { prefix: "TI.", year: "abbreviated", separator: ".", digits: 3 },
    nextNumber: 43,
    version: "AAAAAAAAB9E=",
  });
  assert.deepEqual(
    errorsOf(
      changeNumberSeriesSchema({ ...rules, nextNumberFixed: false }).safeParse(change({ nextNumber: "" })),
    ).fieldErrors,
    { nextNumber: ["Enter the next number."] },
  );
});

test("changeNumberSeriesSchema_AYearThatHasGivenNumbers_SendsNoNextNumber", () => {
  const parsed = changeNumberSeriesSchema({ ...rules, nextNumberFixed: true }).safeParse(
    change({ nextNumber: "" }),
  );

  assert.equal(parsed.success, true);
  assert.equal(parsed.data?.nextNumber, null);
});

test("changeNumberSeriesSchema_AnIdOrVersionThePageDidNotHold_AsksForAReload", () => {
  const { fieldErrors } = errorsOf(
    changeNumberSeriesSchema().safeParse(change({ id: "not-a-numbering", version: "" })),
  );

  assert.deepEqual(fieldErrors, {
    id: ["This page is out of date. Reload the page, then send the form again."],
    version: ["This page is out of date. Reload the page, then send the form again."],
  });
});

test("setUpNumberSeriesValues_ANewNumbering_StartsFromAFormatEveryRuleAllows", () => {
  const values = setUpNumberSeriesValues("test.numbering.notes-02");

  assert.equal(values.documentType, "test.numbering.notes-02");
  assert.equal(setUpNumberSeriesSchema(rules).safeParse(values).success, true);
});

test("changeNumberSeriesValues_EachStateOfTheCurrentYear_OffersTheNextNumberItTakes", () => {
  assert.equal(changeNumberSeriesValues(numbered([])).nextNumber, "1");
  assert.equal(changeNumberSeriesValues(numbered([year()])).nextNumber, "7");
  assert.equal(
    changeNumberSeriesValues(
      numbered([year({ inUse: true, lastIssuedText: "TI.26-27.007", nextText: "TI.26-27.008" })]),
    ).nextNumber,
    "",
  );
  assert.equal(changeNumberSeriesValues(numbered([year({ year: "2025-26" })])).nextNumber, "1");
  assert.deepEqual(changeNumberSeriesValues(numbered([])).format, {
    prefix: "TN-",
    year: "full",
    separator: "-",
    digits: "2",
  });
});

test("nextNumberFixed_OnlyACurrentYearThatHasGivenNumbers_FixesIt", () => {
  assert.equal(nextNumberFixed(numbered([])), false);
  assert.equal(nextNumberFixed(numbered([year()])), false);
  assert.equal(nextNumberFixed(numbered([year({ year: "2025-26", inUse: true })])), false);
  assert.equal(nextNumberFixed(numbered([year({ inUse: true })])), true);
});

test("nextNumberLimit_AFormatTheRuleRefusesOrNoRule_IsWhatTheRequestKeeps", () => {
  const format = { prefix: "TI.", year: "abbreviated", separator: ".", digits: 3 } as const;

  assert.equal(nextNumberLimit(format, rules), 99_999n);
  assert.equal(nextNumberLimit(format, undefined), 9_007_199_254_740_991n);
  assert.equal(nextNumberLimit({ ...format, prefix: "ti." }, rules), 9_007_199_254_740_991n);
  assert.equal(nextNumberLimit(undefined, rules), 9_007_199_254_740_991n);
});
