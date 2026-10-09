import assert from "node:assert/strict";
import { test } from "node:test";

import { personInitials } from "./person-initials.ts";

test("personInitials_FirstAndLastName_TakesTheirFirstLetters", () => {
  assert.equal(personInitials("Asha Rao"), "AR");
  assert.equal(personInitials("asha devi rao"), "AR");
});

test("personInitials_OneName_TakesItsFirstLetter", () => {
  assert.equal(personInitials("Asha"), "A");
});

test("personInitials_ExtraSpaces_AreIgnored", () => {
  assert.equal(personInitials("  Asha   Rao  "), "AR");
});

test("personInitials_LettersOutsideLatin_KeepWholeCharacters", () => {
  assert.equal(personInitials("Ádám Ödön"), "ÁÖ");
  assert.equal(personInitials("𝒜sha 𝒭ao"), "𝒜𝒭");
});

test("personInitials_NameWithCombiningMarks_KeepsTheAccentedLetters", () => {
  assert.equal(personInitials("Ádám Ödön"), "ÁÖ");
});

test("personInitials_NoName_IsEmpty", () => {
  assert.equal(personInitials(""), "");
  assert.equal(personInitials("   "), "");
});
