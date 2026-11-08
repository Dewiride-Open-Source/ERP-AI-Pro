import assert from "node:assert/strict";
import { test } from "node:test";

import type { NumberYearStyle } from "@dewiride/erp-api-client";

import {
  describe,
  fixedLength,
  formatProblems,
  isYearStyle,
  lastNumberWithin,
  renderNumber,
  runningNumberOf,
  sameFormat,
  yearLabel,
  type NumberFormat,
  type SerialNumberRule,
  type TypedFormat,
} from "./number-format.ts";

// The rule is made up, as the API's own tests make it up, so that no statutory value is written here.
const rule: SerialNumberRule = { maxLength: 14, specialCharacters: "-." };

const year = "2026-27";

function format(prefix: string, style: NumberYearStyle, separator: string, digits: number): NumberFormat {
  return { prefix, year: style, separator, digits };
}

test("renderNumber_ANumber_PutsThePrefixTheYearTheSeparatorAndTheRunningNumberPaddedToItsDigits", () => {
  const cases: readonly [NumberFormat, bigint, string][] = [
    [format("TI.", "full", ".", 3), 1n, "TI.2026-27.001"],
    [format("TI.", "abbreviated", "-", 4), 25n, "TI.26-27-0025"],
    [format("TI", "none", "", 5), 7n, "TI00007"],
    [format("", "full", ".", 1), 9n, "2026-27.9"],
    [format("", "none", "", 1), 12345n, "12345"],
    [format("A", "none", "", 2), 1234n, "A1234"],
  ];
  for (const [shape, running, expected] of cases) assert.equal(renderNumber(shape, year, running), expected);
});

test("renderNumber_TheFinancialYear202728_ShowsThatYear", () => {
  assert.equal(renderNumber(format("TI.", "abbreviated", ".", 2), "2027-28", 1n), "TI.27-28.01");
});

test("renderNumber_ARunningNumberBelowOne_Throws", () => {
  assert.throws(() => renderNumber(format("TI", "none", "", 1), year, 0n), RangeError);
});

test("renderNumber_ASeparatorWithoutTheYear_IsLeftOut", () => {
  assert.equal(renderNumber(format("TI", "none", ".", 2), year, 3n), "TI03");
});

test("lastNumberWithin_ALength_IsTheLastRunningNumberWhoseNumberFits", () => {
  const cases: readonly [NumberFormat, number, bigint][] = [
    [format("TI.", "full", ".", 3), 14, 999n],
    [format("TI.", "full", ".", 1), 14, 999n],
    [format("TI", "none", "", 1), 14, 999_999_999_999n],
    [format("TI.", "full", ".", 4), 14, 0n],
    [format("TI", "none", "", 1), 40, 999_999_999_999_999_999n],
    [format("", "none", "", 1), 0, 0n],
  ];
  for (const [shape, maxLength, expected] of cases) {
    assert.equal(lastNumberWithin(shape, maxLength), expected, JSON.stringify({ shape, maxLength }));
  }
});

test("fixedLength_EachYearStyle_CountsThePrefixTheYearAndItsSeparator", () => {
  assert.equal(fixedLength(format("TI.", "full", ".", 3)), 11);
  assert.equal(fixedLength(format("TI.", "abbreviated", "", 3)), 8);
  assert.equal(fixedLength(format("TI.", "none", ".", 3)), 3);
});

test("runningNumberOf_ANumberTheFormatRendered_IsItsRunningNumber", () => {
  const shape = format("TI.", "abbreviated", "-", 2);
  assert.equal(runningNumberOf("TI.26-27-07", shape), 7n);
  assert.equal(runningNumberOf("TI.26-27-1234", shape), 1234n);
  assert.equal(runningNumberOf("999999999999999999", format("", "none", "", 1)), 999_999_999_999_999_999n);
});

test("runningNumberOf_TextWithoutARunningNumber_Throws", () => {
  assert.throws(() => runningNumberOf("TI.26-27-", format("TI.", "abbreviated", "-", 2)), RangeError);
});

test("yearLabel_EachStyle_ShowsTheYearAsANumberDoes", () => {
  assert.deepEqual(
    (["full", "abbreviated", "none"] as const).map((style) => yearLabel(year, style)),
    ["2026-27", "26-27", ""],
  );
});

test("isYearStyle_TheApiValuesAndOthers_AcceptsOnlyTheApiValues", () => {
  assert.deepEqual(["none", "full", "abbreviated", "", "Full", "short"].map(isYearStyle), [
    true,
    true,
    true,
    false,
    false,
    false,
  ]);
});

test("sameFormat_FormatsThatDifferInOnePart_AreNotTheSame", () => {
  const shape = format("TI.", "full", ".", 3);
  assert.equal(sameFormat(shape, { ...shape }), true);
  assert.equal(sameFormat(shape, { ...shape, prefix: "TJ." }), false);
  assert.equal(sameFormat(shape, { ...shape, year: "abbreviated" }), false);
  assert.equal(sameFormat(shape, { ...shape, separator: "-" }), false);
  assert.equal(sameFormat(shape, { ...shape, digits: 4 }), false);
});

test("formatProblems_AFormatTheRuleAllows_HasNone", () => {
  const allowed = [
    format("TI.", "full", ".", 3),
    format("T-I", "abbreviated", "-", 2),
    format("", "full", ".", 2),
    format("", "none", "", 1),
    format("9", "none", "", 6),
  ];
  for (const shape of allowed) assert.equal(formatProblems(shape, rule, year).size, 0, JSON.stringify(shape));
});

test("formatProblems_AFormatTheRuleRefuses_NamesTheFieldAtFault", () => {
  const refused: readonly [TypedFormat, string][] = [
    [format("ti.", "full", ".", 3), "format.prefix"],
    [format("TI/", "full", ".", 3), "format.prefix"],
    [format("TI INV", "none", "", 3), "format.prefix"],
    [format("ABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFG", "none", "", 1), "format.prefix"],
    [{ prefix: "TI", year: "monthly", separator: "", digits: 3 }, "format.year"],
    [format("TI", "full", "/", 3), "format.separator"],
    [format("TI", "full", "..", 3), "format.separator"],
    [format("TI", "none", ".", 3), "format.separator"],
    [format("TI", "none", "", 0), "format.digits"],
    [format("TI", "none", "", 19), "format.digits"],
    [format("TI", "none", "", 2.5), "format.digits"],
    [format("INVOICE.", "full", ".", 3), "format"],
    [format("0A", "none", "", 3), "format.prefix"],
    [format("-A", "none", "", 3), "format.prefix"],
    [format("", "none", "", 3), "format.digits"],
  ];
  for (const [shape, field] of refused) {
    const problems = formatProblems(shape, rule, year);
    assert.deepEqual([...problems.keys()], [field], JSON.stringify(shape));
  }
});

test("formatProblems_EachRefusal_SaysWhatTheApiSays", () => {
  assert.equal(
    formatProblems(format("ti", "none", "", 3), rule, year).get("format.prefix"),
    'Use at most 32 characters, each a capital letter, a digit or "-" or ".".',
  );
  assert.equal(
    formatProblems(format("TI", "full", "/", 3), rule, year).get("format.separator"),
    'Use "-" or ".", or nothing.',
  );
  assert.equal(
    formatProblems(format("TI", "none", ".", 3), rule, year).get("format.separator"),
    "The separator follows the financial year, so leave it empty when the year is not shown.",
  );
  assert.equal(
    formatProblems(format("TI", "none", "", 0), rule, year).get("format.digits"),
    "Use 1 to 18 digits for the running number.",
  );
  assert.equal(
    formatProblems(format("INVOICE.", "full", ".", 3), rule, year).get("format"),
    "The first number, INVOICE.2026-27.001, has 19 characters; a number may have at most 14.",
  );
  assert.equal(
    formatProblems(format("", "none", "", 3), rule, year).get("format.digits"),
    "A number must begin with a letter or a digit other than 0, and 001 does not.",
  );
});

test("formatProblems_ARuleWithoutTheYearsHyphen_RefusesTheYear", () => {
  const withoutHyphen: SerialNumberRule = { maxLength: 14, specialCharacters: "." };
  assert.equal(
    formatProblems(format("TI", "abbreviated", "", 3), withoutHyphen, year).get("format.year"),
    'The financial year is written with "-", which a number may not contain, so leave the year out.',
  );
  assert.equal(formatProblems(format("TI", "none", "", 3), withoutHyphen, year).size, 0);
});

test("formatProblems_AYearThatBeginsTheNumber_IsAllowed", () => {
  assert.equal(formatProblems(format("", "abbreviated", "-", 3), rule, year).size, 0);
});

test("describe_TheRulesCharacters_QuotesEachOne", () => {
  assert.equal(describe("-."), '"-" or "."');
  assert.equal(describe(""), "no other character");
});
