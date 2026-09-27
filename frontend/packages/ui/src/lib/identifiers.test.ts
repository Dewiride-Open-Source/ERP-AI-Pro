import assert from "node:assert/strict";
import { test } from "node:test";

import { normaliseIdentifier } from "./identifiers.ts";

test("normaliseIdentifier_PastedWithSpacesAndLowerCase_UpperCasesAndJoins", () => {
  assert.equal(normaliseIdentifier("27 abcde 1234f 1z5", 15), "27ABCDE1234F1Z5");
});

test("normaliseIdentifier_Punctuation_IsRemoved", () => {
  assert.equal(normaliseIdentifier("abcde-1234-f", 10), "ABCDE1234F");
  assert.equal(normaliseIdentifier("hdfc/0001234.", 11), "HDFC0001234");
});

test("normaliseIdentifier_LongerThanTheLimit_StopsAtTheLimit", () => {
  assert.equal(normaliseIdentifier("27 abcde 1234f 1z5 99", 15), "27ABCDE1234F1Z5");
  assert.equal(normaliseIdentifier("ABCDE1234FXYZ", 10), "ABCDE1234F");
});

test("normaliseIdentifier_LettersOutsideAsciiAndOtherScripts_AreRemoved", () => {
  assert.equal(normaliseIdentifier("straße", 10), "STRAE");
  assert.equal(normaliseIdentifier("१२AB３４", 10), "AB");
});

test("normaliseIdentifier_AnyPrefix_NormalisesToAPrefixOfTheWhole", () => {
  const text = "27 abcde 1234f 1z5 extra";
  const whole = normaliseIdentifier(text, 15);
  for (let cut = 0; cut <= text.length; cut += 1) {
    assert.ok(whole.startsWith(normaliseIdentifier(text.slice(0, cut), 15)), `cut at ${cut}`);
  }
});

test("normaliseIdentifier_EmptyText_ReturnsEmpty", () => {
  assert.equal(normaliseIdentifier("", 15), "");
  assert.equal(normaliseIdentifier(" -/ ", 15), "");
});

test("normaliseIdentifier_LimitBelowOneOrFractional_Throws", () => {
  assert.throws(() => normaliseIdentifier("A", 0), RangeError);
  assert.throws(() => normaliseIdentifier("A", 2.5), RangeError);
});
