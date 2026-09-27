import assert from "node:assert/strict";
import { test } from "node:test";

import {
  groupIndian,
  maxAmountScale,
  normaliseAmount,
  sanitiseAmountText,
  ungroupedOffset,
  type AmountFormat,
} from "./indian-number.ts";

const rupees: AmountFormat = { scale: 2, allowNegative: false };
const signedRupees: AmountFormat = { scale: 2, allowNegative: true };
const wholeUnits: AmountFormat = { scale: 0, allowNegative: false };

const minusSign = String.fromCodePoint(0x2212);
const enDash = String.fromCodePoint(0x2013);
const smallHyphenMinus = String.fromCodePoint(0xfe63);
const fullwidthHyphenMinus = String.fromCodePoint(0xff0d);

function typeIntoAmount(keys: string, format: AmountFormat): string {
  let text = "";
  for (const key of keys) text = sanitiseAmountText(text + key, format);
  return text;
}

test("sanitiseAmountText_PastedGroupedRupees_KeepsDigitsAndThePoint", () => {
  assert.equal(sanitiseAmountText("₹ 1,23,45,678.90", rupees), "12345678.90");
  assert.equal(sanitiseAmountText("Rs. 2,500/-", rupees), "2500");
  assert.equal(sanitiseAmountText("INR 2,500.00", rupees), "2500.00");
});

test("sanitiseAmountText_LeadingPoint_StartsTheFraction", () => {
  assert.equal(sanitiseAmountText(".5", rupees), ".5");
  assert.equal(sanitiseAmountText("-.5", signedRupees), "-.5");
  assert.equal(sanitiseAmountText("₹.75", rupees), ".75");
});

test("sanitiseAmountText_Letters_AreDropped", () => {
  assert.equal(sanitiseAmountText("12a3b", rupees), "123");
  assert.equal(sanitiseAmountText("1e7", rupees), "17");
});

test("sanitiseAmountText_SecondPoint_IsDropped", () => {
  assert.equal(sanitiseAmountText("1.2.3", rupees), "1.23");
});

test("sanitiseAmountText_MoreDecimalsThanTheScale_AreDropped", () => {
  assert.equal(sanitiseAmountText("12.345", rupees), "12.34");
  assert.equal(sanitiseAmountText("0.12345", { scale: 4, allowNegative: false }), "0.1234");
});

test("sanitiseAmountText_ScaleZero_KeepsThePointAndDropsTheFraction", () => {
  assert.equal(sanitiseAmountText("1500.75", wholeUnits), "1500.");
  assert.equal(sanitiseAmountText("₹25,00,000.00", wholeUnits), "2500000.");
  assert.deepEqual(normaliseAmount(sanitiseAmountText("₹25,00,000.00", wholeUnits), wholeUnits), {
    canonical: "2500000",
  });
});

test("sanitiseAmountText_TypedOneKeyAtATime_KeepsTheSizeOfTheAmount", () => {
  assert.equal(typeIntoAmount("1500.50", wholeUnits), "1500.");
  assert.equal(typeIntoAmount("1500.505", rupees), "1500.50");
  assert.equal(typeIntoAmount(`${minusSign}25.5`, signedRupees), "-25.5");
});

test("sanitiseAmountText_MinusSignsAndParentheses_BecomeALeadingMinus", () => {
  const cases: [string, string][] = [
    ["-1500", "-1500"],
    [`${minusSign}2,500.00`, "-2500.00"],
    [`${enDash}2,500.00`, "-2500.00"],
    [`${smallHyphenMinus}2,500.00`, "-2500.00"],
    [`${fullwidthHyphenMinus}2,500.00`, "-2500.00"],
    ["(2,500.00)", "-2500.00"],
    ["₹(2,500.00)", "-2500.00"],
    [`₹ ${minusSign}2,500`, "-2500"],
    ["Rs. (2,500)", "-2500"],
  ];
  for (const [text, expected] of cases) {
    assert.equal(sanitiseAmountText(text, signedRupees), expected, JSON.stringify(text));
    assert.deepEqual(normaliseAmount(expected, signedRupees), { canonical: expected }, JSON.stringify(text));
  }
});

test("sanitiseAmountText_MinusAfterADigitOrASecondMinus_IsDropped", () => {
  assert.equal(sanitiseAmountText("--15-00", signedRupees), "-1500");
  assert.equal(sanitiseAmountText("15-00", signedRupees), "1500");
  assert.equal(sanitiseAmountText("(-2500)", signedRupees), "-2500");
  assert.equal(sanitiseAmountText(`2500${minusSign}`, signedRupees), "2500");
});

test("sanitiseAmountText_MinusWhenNegativesAreRefused_IsKeptSoTheAmountIsInvalid", () => {
  for (const text of ["-1500", `${minusSign}1,500`, "(1,500.00)"]) {
    const sanitised = sanitiseAmountText(text, rupees);
    assert.ok(sanitised.startsWith("-"), JSON.stringify(text));
    assert.deepEqual(normaliseAmount(sanitised, rupees), { invalid: true }, JSON.stringify(text));
  }
});

test("sanitiseAmountText_MoreThanFifteenIntegerDigits_KeepsThemSoTheAmountIsInvalid", () => {
  const pasted = sanitiseAmountText("1,23,45,67,89,01,23,456.00", rupees);
  assert.equal(pasted, "1234567890123456.00");
  assert.deepEqual(normaliseAmount(pasted, rupees), { invalid: true });
  assert.equal(sanitiseAmountText("1234567890123456789", rupees), "1234567890123456789");
  assert.equal(sanitiseAmountText("000999999999999999", rupees), "000999999999999999");
  assert.deepEqual(normaliseAmount("000999999999999999", rupees), { canonical: "999999999999999" });
});

test("sanitiseAmountText_AnyPrefix_SanitisesToAPrefixOfTheWhole", () => {
  const alphabet = `0123456789.-,₹ ae()${minusSign}${enDash}`;
  let seed = 20260927;
  const next = () => {
    seed = (seed * 1103515245 + 12345) % 2147483648;
    return seed;
  };
  for (let sample = 0; sample < 500; sample += 1) {
    let text = "";
    const length = next() % 24;
    for (let index = 0; index < length; index += 1) text += alphabet[next() % alphabet.length];
    for (const format of [rupees, signedRupees, wholeUnits]) {
      const whole = sanitiseAmountText(text, format);
      for (let cut = 0; cut <= text.length; cut += 1) {
        assert.ok(
          whole.startsWith(sanitiseAmountText(text.slice(0, cut), format)),
          `${JSON.stringify(text)} cut at ${cut}`,
        );
      }
    }
  }
});

test("sanitiseAmountText_ScaleOutOfRange_Throws", () => {
  assert.throws(
    () => sanitiseAmountText("1", { scale: maxAmountScale + 1, allowNegative: false }),
    RangeError,
  );
  assert.throws(() => sanitiseAmountText("1", { scale: 1.5, allowNegative: false }), RangeError);
  assert.throws(() => sanitiseAmountText("1", { scale: -1, allowNegative: false }), RangeError);
});

test("normaliseAmount_TypedText_ReturnsTheCanonicalDecimal", () => {
  const cases: [string, AmountFormat, string][] = [
    ["", rupees, ""],
    [".", rupees, ""],
    ["-", signedRupees, ""],
    ["-.", signedRupees, ""],
    ["0", rupees, "0"],
    ["000", rupees, "0"],
    ["0012.50", rupees, "12.50"],
    ["12.", rupees, "12"],
    [".5", rupees, "0.5"],
    ["-.5", signedRupees, "-0.5"],
    ["-0", signedRupees, "0"],
    ["-0.00", signedRupees, "0.00"],
    ["-0012", signedRupees, "-12"],
    ["1234567.50", rupees, "1234567.50"],
    ["999999999999999.99", rupees, "999999999999999.99"],
    ["0000999999999999999", rupees, "999999999999999"],
    ["1500", wholeUnits, "1500"],
    ["1500.", wholeUnits, "1500"],
  ];
  for (const [text, format, canonical] of cases) {
    assert.deepEqual(normaliseAmount(text, format), { canonical }, JSON.stringify(text));
  }
});

test("normaliseAmount_TextOutsideTheGrammar_IsInvalid", () => {
  const cases: [string, AmountFormat][] = [
    ["1,500", rupees],
    ["₹1500", rupees],
    [" 1500", rupees],
    ["1e7", rupees],
    ["1.2.3", rupees],
    ["--1", signedRupees],
    ["1-", signedRupees],
    ["-", rupees],
    ["-1", rupees],
    ["12.345", rupees],
    ["12.5", wholeUnits],
    ["1234567890123456", rupees],
  ];
  for (const [text, format] of cases) {
    assert.deepEqual(normaliseAmount(text, format), { invalid: true }, JSON.stringify(text));
  }
});

test("groupIndian_CanonicalAmount_GroupsInLakhsAndCrores", () => {
  const cases: [string, number, string][] = [
    ["", 2, ""],
    ["0", 2, "0.00"],
    ["999", 2, "999.00"],
    ["1000", 2, "1,000.00"],
    ["100000", 2, "1,00,000.00"],
    ["9999999", 0, "99,99,999"],
    ["10000000", 2, "1,00,00,000.00"],
    ["12345678.5", 2, "1,23,45,678.50"],
    ["-1500", 2, "-1,500.00"],
    ["-1234567.89", 2, "-12,34,567.89"],
    ["999999999999999.99", 2, "99,99,99,99,99,99,999.99"],
    ["0.1234", 4, "0.1234"],
  ];
  for (const [canonical, scale, expected] of cases) {
    assert.equal(groupIndian(canonical, scale), expected, canonical);
  }
});

test("groupIndian_AgreesWithIntlEnInOnExactDecimalStrings", () => {
  const values = [
    "1",
    "12",
    "123",
    "1234",
    "12345",
    "123456",
    "1234567",
    "12345678",
    "123456789",
    "9007199254740993",
    "123456789012345.67",
    "-98765432109876.5",
    "-0.01",
  ];
  for (const scale of [0, 2, 4]) {
    const format = new Intl.NumberFormat("en-IN", {
      minimumFractionDigits: scale,
      maximumFractionDigits: scale,
      roundingMode: "trunc",
    });
    for (const value of values) {
      const truncated = scale === 0 ? value.replace(/\..*$/, "") : value;
      if (truncated === "-0") continue;
      assert.equal(
        groupIndian(truncated, scale),
        format.format(truncated as `${number}`),
        `${truncated} at scale ${scale}`,
      );
    }
  }
});

test("groupIndian_TextThatIsNotCanonical_IsReturnedUnchanged", () => {
  assert.equal(groupIndian("abc", 2), "abc");
  assert.equal(groupIndian("1,500", 2), "1,500");
});

test("ungroupedOffset_CaretInGroupedText_CountsOnlyTheCharactersKeptInThePlainText", () => {
  assert.equal(ungroupedOffset("1,23,456.00", 0, "123456"), 0);
  assert.equal(ungroupedOffset("1,23,456.00", 2, "123456"), 1);
  assert.equal(ungroupedOffset("1,23,456.00", 5, "123456"), 3);
  assert.equal(ungroupedOffset("-1,500.00", 3, "-1500.00"), 2);
});

test("ungroupedOffset_CaretInThePaddedFraction_StopsAtTheEndOfThePlainText", () => {
  assert.equal(ungroupedOffset("1,23,456.00", 11, "123456"), 6);
  assert.equal(ungroupedOffset("1,23,456.50", 11, "123456.5"), 8);
});
