import assert from "node:assert/strict";
import { test } from "node:test";

import { formatRupees } from "./money.ts";

test("formatRupees_Amount_UsesLakhAndCroreGroupingWithPaise", () => {
  assert.equal(formatRupees(118000), "₹1,18,000.00");
  assert.equal(formatRupees(12345678.9), "₹1,23,45,678.90");
  assert.equal(formatRupees(0.5), "₹0.50");
});

test("formatRupees_WithoutPaise_RoundsToWholeRupees", () => {
  assert.equal(formatRupees(25000, { paise: false }), "₹25,000");
  assert.equal(formatRupees(80000.4, { paise: false }), "₹80,000");
});

test("formatRupees_NegativeAmount_KeepsTheSign", () => {
  assert.equal(formatRupees(-1500), "-₹1,500.00");
});
