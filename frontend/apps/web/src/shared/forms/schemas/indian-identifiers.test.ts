import assert from "node:assert/strict";
import { test } from "node:test";

import type { z } from "zod";

import { gstinSchema, identifierLengths, ifscSchema, panSchema } from "./indian-identifiers.ts";

function messages(schema: z.ZodType, value: unknown): string[] {
  const result = schema.safeParse(value);
  return result.success ? [] : result.error.issues.map((issue) => issue.message);
}

test("gstinSchema_TypedWithSpacesAndLowerCase_IsTheCanonicalGstin", () => {
  assert.equal(gstinSchema.parse("27 abcde 1234f 1z5"), "27ABCDE1234F1Z5");
  assert.equal(gstinSchema.parse("29-AAAAA-1303-P1ZV"), "29AAAAA1303P1ZV");
});

test("gstinSchema_LetterOutsideBasicLatin_IsDroppedLikeTheInputDropsIt", () => {
  const dotlessI = String.fromCodePoint(0x131);
  const sharpS = String.fromCodePoint(0xdf);

  assert.deepEqual(messages(gstinSchema, `27abcde1234f1z${dotlessI}`), [
    "Enter a 15-character GSTIN: two digits, then 13 letters or digits.",
  ]);
  assert.deepEqual(messages(gstinSchema, `27abcde1234f1${sharpS}`), [
    "Enter a 15-character GSTIN: two digits, then 13 letters or digits.",
  ]);
  assert.equal(gstinSchema.parse(`27abcde1234f1z${dotlessI}5`), "27ABCDE1234F1Z5");
});

test("gstinSchema_AnyEntityCodeAndCheckCharacter_IsAccepted", () => {
  for (const gstin of ["29AAAAA1303P1ZV", "07ABCDE1234F2DX", "99ABCDE1234F9X9", "0512345678901AB"]) {
    assert.equal(gstinSchema.parse(gstin), gstin);
  }
});

test("gstinSchema_Empty_AsksForTheGstinOnce", () => {
  assert.deepEqual(messages(gstinSchema, ""), ["Enter the GSTIN."]);
  assert.deepEqual(messages(gstinSchema, " - "), ["Enter the GSTIN."]);
  assert.deepEqual(messages(gstinSchema, undefined), ["Enter the GSTIN."]);
});

test("gstinSchema_WrongShape_DescribesTheShape", () => {
  for (const gstin of ["2AABCDE1234F1Z5", "27ABCDE1234F1Z", "27ABCDE1234F1Z55", "A7ABCDE1234F1Z5"]) {
    assert.deepEqual(
      messages(gstinSchema, gstin),
      ["Enter a 15-character GSTIN: two digits, then 13 letters or digits."],
      gstin,
    );
  }
});

test("panSchema_TypedInLowerCase_IsTheCanonicalPan", () => {
  assert.equal(panSchema.parse("abcde1234f"), "ABCDE1234F");
  assert.equal(panSchema.parse("ABCDE 1234 F"), "ABCDE1234F");
});

test("panSchema_WrongShape_DescribesTheShape", () => {
  for (const pan of ["ABCD12345F", "ABCDE1234", "ABCDE12345", "12345ABCDE", "ABCDE1234FG"]) {
    assert.deepEqual(
      messages(panSchema, pan),
      ["Enter a 10-character PAN: five letters, four digits, then a letter."],
      pan,
    );
  }
  assert.deepEqual(messages(panSchema, ""), ["Enter the PAN."]);
});

test("ifscSchema_TypedInLowerCase_IsTheCanonicalIfsc", () => {
  assert.equal(ifscSchema.parse("sbin0001234"), "SBIN0001234");
  assert.equal(ifscSchema.parse("HDFC0ABC123"), "HDFC0ABC123");
});

test("ifscSchema_WrongShape_DescribesTheShape", () => {
  for (const ifsc of ["SBIN1001234", "SBI00001234", "SBIN000123", "SBIN00012345", "1BIN0001234"]) {
    assert.deepEqual(
      messages(ifscSchema, ifsc),
      ["Enter an 11-character IFSC: four letters, a zero, then six letters or digits."],
      ifsc,
    );
  }
  assert.deepEqual(messages(ifscSchema, ""), ["Enter the IFSC."]);
});

test("identifierLengths_EachIdentifier_MatchesItsShape", () => {
  assert.equal(gstinSchema.parse("29AAAAA1303P1ZV").length, identifierLengths.gstin);
  assert.equal(panSchema.parse("ABCDE1234F").length, identifierLengths.pan);
  assert.equal(ifscSchema.parse("SBIN0001234").length, identifierLengths.ifsc);
});
