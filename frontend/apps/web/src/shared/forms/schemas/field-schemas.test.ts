import assert from "node:assert/strict";
import { test } from "node:test";

import { z } from "zod";

import {
  amountSchema,
  calendarDateSchema,
  dateRangeSchema,
  optionalInput,
  requiredText,
} from "./field-schemas.ts";

type Reported = { readonly path: string; readonly message: string };

function issues(schema: z.ZodType, value: unknown): Reported[] {
  const result = schema.safeParse(value);
  return result.success
    ? []
    : result.error.issues.map((issue) => ({ path: issue.path.join("."), message: issue.message }));
}

function messages(schema: z.ZodType, value: unknown): string[] {
  return issues(schema, value).map((issue) => issue.message);
}

test("requiredText_TextWithSurroundingSpace_IsTrimmed", () => {
  assert.equal(requiredText(100).parse("  Dewiride Technologies  "), "Dewiride Technologies");
});

test("requiredText_EmptyOrBlank_AsksForAValueOnce", () => {
  assert.deepEqual(messages(requiredText(100), ""), ["Enter a value."]);
  assert.deepEqual(messages(requiredText(100), "   "), ["Enter a value."]);
  assert.deepEqual(messages(requiredText(100, { required: "Enter the legal name." }), undefined), [
    "Enter the legal name.",
  ]);
});

test("requiredText_LongerThanTheLimit_NamesTheLimit", () => {
  assert.deepEqual(messages(requiredText(5), "Dewiride"), ["Use 5 characters or fewer."]);
  assert.deepEqual(messages(requiredText(5, { tooLong: "Shorten the name." }), "Dewiride"), [
    "Shorten the name.",
  ]);
  assert.equal(requiredText(5).parse(" Dewi "), "Dewi");
});

test("optionalInput_EmptyOrBlank_IsAbsent", () => {
  const schema = optionalInput(requiredText(10));

  assert.equal(schema.parse(""), undefined);
  assert.equal(schema.parse("   "), undefined);
});

test("optionalInput_AbsentValue_IsAbsent", () => {
  const schema = optionalInput(requiredText(10));

  assert.equal(schema.parse(undefined), undefined);
  assert.equal(schema.parse(null), undefined);
});

test("optionalInput_AnyOtherText_MustSatisfyTheSchema", () => {
  const schema = optionalInput(requiredText(3));

  assert.equal(schema.parse(" abc "), "abc");
  assert.deepEqual(messages(schema, "abcd"), ["Use 3 characters or fewer."]);
});

test("amountSchema_TypedAmount_IsTheCanonicalDecimalString", () => {
  const schema = amountSchema({ scale: 2, required: true });
  const table: readonly (readonly [string, string])[] = [
    ["1234567.50", "1234567.50"],
    ["007.5", "7.5"],
    ["12.", "12"],
    [".5", "0.5"],
    ["0", "0"],
    ["1,23,456.00", "123456.00"],
    ["₹ 1,000", "1000"],
    [" 250 ", "250"],
    ["-0", "0"],
    ["999999999999999.99", "999999999999999.99"],
  ];

  for (const [typed, canonical] of table) {
    assert.equal(schema.parse(typed), canonical, typed);
  }
});

test("amountSchema_Empty_AsksForTheAmount", () => {
  assert.deepEqual(messages(amountSchema({ scale: 2, required: true }), ""), ["Enter an amount."]);
  assert.deepEqual(
    messages(amountSchema({ scale: 2, required: true, requiredMessage: "Enter the opening balance." }), " "),
    ["Enter the opening balance."],
  );
});

test("amountSchema_TextThatIsNotAnAmount_AsksForFigures", () => {
  for (const typed of ["abc", "1.2.3", "-", ".", "1e5", "12-", "--1", "0x10"]) {
    assert.deepEqual(
      messages(amountSchema({ scale: 2, required: true }), typed),
      ["Enter the amount in figures, for example 1250.50."],
      typed,
    );
  }
});

test("amountSchema_TooManyDigitsBeforeThePoint_IsRefused", () => {
  assert.deepEqual(messages(amountSchema({ scale: 2, required: true }), "1000000000000000"), [
    "Enter an amount with at most 15 digits before the decimal point.",
  ]);
});

test("amountSchema_TooManyDigitsBehindLeadingZeros_NamesTheDigitLimit", () => {
  assert.deepEqual(messages(amountSchema({ scale: 2, required: true }), "0001000000000000000"), [
    "Enter an amount with at most 15 digits before the decimal point.",
  ]);
  assert.deepEqual(messages(amountSchema({ scale: 2, required: true }), "0012.34567"), [
    "Enter at most 2 digits after the decimal point.",
  ]);
});

test("amountSchema_TooManyDigitsAfterThePoint_NamesTheScale", () => {
  assert.deepEqual(messages(amountSchema({ scale: 2, required: true }), "1.234"), [
    "Enter at most 2 digits after the decimal point.",
  ]);
  assert.deepEqual(messages(amountSchema({ scale: 0, required: true }), "1.5"), [
    "Enter a whole amount, without paise.",
  ]);
  assert.equal(amountSchema({ scale: 4, required: true }).parse("0.1234"), "0.1234");
});

test("amountSchema_NegativeAmount_IsRefusedUnlessAllowed", () => {
  assert.deepEqual(messages(amountSchema({ scale: 2, required: true }), "-1"), [
    "Enter an amount of zero or more.",
  ]);
  assert.equal(amountSchema({ scale: 2, required: true, allowNegative: true }).parse("-1500.50"), "-1500.50");
});

test("amountSchema_OutsideItsBounds_NamesTheBoundInRupees", () => {
  const schema = amountSchema({ scale: 2, required: true, min: "0.01", max: "100000" });

  assert.deepEqual(messages(schema, "0"), ["Enter an amount of at least ₹0.01."]);
  assert.deepEqual(messages(schema, "100000.01"), ["Enter an amount of at most ₹1,00,000.00."]);
  assert.equal(schema.parse("0.01"), "0.01");
  assert.equal(schema.parse("100000.00"), "100000.00");
});

test("amountSchema_NegativeBound_IsShownWithItsSign", () => {
  const schema = amountSchema({ scale: 2, required: true, allowNegative: true, min: "-500" });

  assert.deepEqual(messages(schema, "-500.01"), ["Enter an amount of at least -₹500.00."]);
  assert.equal(schema.parse("-500"), "-500");
});

test("amountSchema_AmountAndBoundOfOppositeSigns_AreComparedBySignFirst", () => {
  const aboveNegativeMinimum = amountSchema({ scale: 2, required: true, allowNegative: true, min: "-500" });
  const acrossZero = amountSchema({ scale: 2, required: true, allowNegative: true, min: "-10", max: "10" });

  assert.equal(aboveNegativeMinimum.parse("100"), "100");
  assert.equal(aboveNegativeMinimum.parse("0"), "0");
  assert.equal(acrossZero.parse("0"), "0");
  assert.equal(acrossZero.parse("-0"), "0");
  assert.equal(acrossZero.parse("-9.99"), "-9.99");
  assert.equal(acrossZero.parse("10"), "10");
  assert.deepEqual(messages(acrossZero, "10.01"), ["Enter an amount of at most ₹10.00."]);
  assert.deepEqual(messages(acrossZero, "-10.01"), ["Enter an amount of at least -₹10.00."]);
});

test("amountSchema_BoundsOfOppositeSigns_AreCheckedForOrderWhenTheSchemaIsBuilt", () => {
  assert.doesNotThrow(() =>
    amountSchema({ scale: 2, required: true, allowNegative: true, min: "-5", max: "5" }),
  );
  assert.throws(
    () => amountSchema({ scale: 2, required: true, allowNegative: true, min: "5", max: "-5" }),
    RangeError,
  );
});

test("amountSchema_BoundsBeyondTheExactRangeOfNumbers_AreComparedExactly", () => {
  const schema = amountSchema({ scale: 2, required: true, max: "12345678901234.5" });

  assert.deepEqual(messages(schema, "12345678901234.51"), [
    "Enter an amount of at most ₹1,23,45,67,89,01,234.50.",
  ]);
  assert.equal(schema.parse("12345678901234.49"), "12345678901234.49");
  assert.equal(schema.parse("12345678901234.50"), "12345678901234.50");
});

test("amountSchema_Optional_IsAbsentWhenEmpty", () => {
  const schema = amountSchema({ scale: 2, required: false });

  assert.equal(schema.parse(""), undefined);
  assert.equal(schema.parse("  "), undefined);
  assert.equal(schema.parse("12.50"), "12.50");
  assert.deepEqual(messages(schema, "twelve"), ["Enter the amount in figures, for example 1250.50."]);
});

test("amountSchema_OptionalAndAbsent_IsAbsent", () => {
  const schema = amountSchema({ scale: 2, required: false, max: "100" });

  assert.equal(schema.parse(undefined), undefined);
  assert.equal(schema.parse(null), undefined);
});

test("amountSchema_RequiredAndAbsent_AsksForTheAmount", () => {
  assert.deepEqual(messages(amountSchema({ scale: 2, required: true }), undefined), ["Enter an amount."]);
  assert.deepEqual(messages(amountSchema({ scale: 2, required: true }), null), ["Enter an amount."]);
});

test("amountSchema_OptionsThatCannotDescribeAnAmount_AreRefusedWhenTheSchemaIsBuilt", () => {
  assert.throws(() => amountSchema({ scale: 5, required: true }), RangeError);
  assert.throws(() => amountSchema({ scale: 1.5, required: true }), RangeError);
  assert.throws(() => amountSchema({ scale: 2, required: true, min: "1.234" }), RangeError);
  assert.throws(() => amountSchema({ scale: 2, required: true, min: "01" }), RangeError);
  assert.throws(() => amountSchema({ scale: 2, required: true, min: "-1" }), RangeError);
  assert.throws(() => amountSchema({ scale: 2, required: true, min: "10", max: "9.99" }), RangeError);
});

test("calendarDateSchema_IsoCalendarDate_IsAccepted", () => {
  const schema = calendarDateSchema({ required: true });

  for (const date of ["2026-03-31", "2024-02-29", "2000-02-29", "0001-01-01", "9999-12-31"]) {
    assert.equal(schema.parse(date), date);
  }
  assert.equal(schema.parse(" 2026-04-01 "), "2026-04-01");
});

test("calendarDateSchema_DateThatDoesNotExist_AsksForARealDate", () => {
  const schema = calendarDateSchema({ required: true });

  for (const date of [
    "2026-02-29",
    "1900-02-29",
    "2026-04-31",
    "2026-13-01",
    "2026-00-10",
    "2026-01-00",
    "2026-1-5",
    "0000-01-01",
    "0000-02-29",
    "0000-06-15",
  ]) {
    assert.deepEqual(
      messages(schema, date),
      ["Enter a real date as day-month-year, for example 31-03-2026."],
      date,
    );
  }
});

test("calendarDateSchema_TypedTextTheInputCouldNotRead_AsksForARealDate", () => {
  assert.deepEqual(messages(calendarDateSchema({ required: true }), "31-02-2026"), [
    "Enter a real date as day-month-year, for example 31-03-2026.",
  ]);
});

test("calendarDateSchema_Empty_AsksForTheDate", () => {
  assert.deepEqual(messages(calendarDateSchema({ required: true }), ""), ["Enter a date."]);
  assert.deepEqual(
    messages(calendarDateSchema({ required: true, requiredMessage: "Enter the agreement date." }), " "),
    ["Enter the agreement date."],
  );
});

test("calendarDateSchema_OutsideItsBounds_NamesTheBoundAsDayMonthYear", () => {
  const schema = calendarDateSchema({ required: true, min: "2026-04-01", max: "2027-03-31" });

  assert.deepEqual(messages(schema, "2026-03-31"), ["Enter a date on or after 01-04-2026."]);
  assert.deepEqual(messages(schema, "2027-04-01"), ["Enter a date on or before 31-03-2027."]);
  assert.equal(schema.parse("2026-04-01"), "2026-04-01");
  assert.equal(schema.parse("2027-03-31"), "2027-03-31");
});

test("calendarDateSchema_Optional_IsAbsentWhenEmpty", () => {
  const schema = calendarDateSchema({ required: false });

  assert.equal(schema.parse(""), undefined);
  assert.equal(schema.parse("2026-04-01"), "2026-04-01");
});

test("calendarDateSchema_OptionalAndAbsent_IsAbsent", () => {
  const schema = calendarDateSchema({ required: false, min: "2026-04-01" });

  assert.equal(schema.parse(undefined), undefined);
  assert.equal(schema.parse(null), undefined);
});

test("calendarDateSchema_RequiredAndAbsent_AsksForTheDate", () => {
  assert.deepEqual(messages(calendarDateSchema({ required: true }), undefined), ["Enter a date."]);
});

test("dateRangeSchema_OptionalRangeWithAbsentEnds_IsAbsent", () => {
  assert.deepEqual(dateRangeSchema({ required: false }).parse({ from: undefined, to: null }), {
    from: undefined,
    to: undefined,
  });
});

test("calendarDateSchema_BoundsThatAreNotDates_AreRefusedWhenTheSchemaIsBuilt", () => {
  assert.throws(() => calendarDateSchema({ required: true, min: "2026-02-30" }), RangeError);
  assert.throws(() => calendarDateSchema({ required: true, max: "31-03-2027" }), RangeError);
  assert.throws(() => calendarDateSchema({ required: true, min: "0000-01-01" }), RangeError);
  assert.throws(
    () => calendarDateSchema({ required: true, min: "2027-01-01", max: "2026-12-31" }),
    RangeError,
  );
});

test("dateRangeSchema_EndOnOrAfterTheStart_IsAccepted", () => {
  const schema = dateRangeSchema({ required: true });

  assert.deepEqual(schema.parse({ from: "2026-04-01", to: "2027-03-31" }), {
    from: "2026-04-01",
    to: "2027-03-31",
  });
  assert.deepEqual(schema.parse({ from: "2026-04-01", to: "2026-04-01" }), {
    from: "2026-04-01",
    to: "2026-04-01",
  });
});

test("dateRangeSchema_EndBeforeTheStart_IsReportedOnTheEnd", () => {
  assert.deepEqual(issues(dateRangeSchema({ required: true }), { from: "2026-04-01", to: "2026-03-31" }), [
    { path: "to", message: "The end date must be on or after the start date." },
  ]);
});

test("dateRangeSchema_EmptyRequiredRange_AsksForBothDates", () => {
  assert.deepEqual(issues(dateRangeSchema({ required: true }), { from: "", to: "" }), [
    { path: "from", message: "Enter the start date." },
    { path: "to", message: "Enter the end date." },
  ]);
});

test("dateRangeSchema_EndThatIsNotADate_IsNotComparedWithTheStart", () => {
  assert.deepEqual(issues(dateRangeSchema({ required: true }), { from: "2026-04-01", to: "2026-02-30" }), [
    { path: "to", message: "Enter a real date as day-month-year, for example 31-03-2026." },
  ]);
});

test("dateRangeSchema_OptionalRange_AllowsEitherEndToBeEmpty", () => {
  const schema = dateRangeSchema({ required: false });

  assert.deepEqual(schema.parse({ from: "", to: "" }), { from: undefined, to: undefined });
  assert.deepEqual(schema.parse({ from: "2026-04-01", to: "" }), { from: "2026-04-01", to: undefined });
  assert.deepEqual(issues(schema, { from: "2026-04-01", to: "2026-03-01" }), [
    { path: "to", message: "The end date must be on or after the start date." },
  ]);
});

test("dateRangeSchema_Bounds_ApplyToBothEnds", () => {
  const schema = dateRangeSchema({ required: true, min: "2026-04-01", max: "2027-03-31" });

  assert.deepEqual(issues(schema, { from: "2026-03-01", to: "2027-04-30" }), [
    { path: "from", message: "Enter a date on or after 01-04-2026." },
    { path: "to", message: "Enter a date on or before 31-03-2027." },
  ]);
});

test("fieldSchemas_RequiredOrOptional_ShapesTheParsedValue", () => {
  const amount: string = amountSchema({ scale: 2, required: true }).parse("1");
  const noAmount: string | undefined = amountSchema({ scale: 2, required: false }).parse("");
  const date: string = calendarDateSchema({ required: true }).parse("2026-04-01");
  const range: { readonly from: string; readonly to: string } = dateRangeSchema({ required: true }).parse({
    from: "2026-04-01",
    to: "2026-04-30",
  });

  assert.equal(amount, "1");
  assert.equal(noAmount, undefined);
  assert.equal(date, "2026-04-01");
  assert.deepEqual(range, { from: "2026-04-01", to: "2026-04-30" });
});

test("amountSchema_InsideAnObject_ReportsTheFieldsPath", () => {
  const schema = z.object({
    lines: z.array(z.object({ amount: amountSchema({ scale: 2, required: true }) })),
  });

  assert.deepEqual(issues(schema, { lines: [{ amount: "1" }, { amount: "" }] }), [
    { path: "lines.1.amount", message: "Enter an amount." },
  ]);
});
