import assert from "node:assert/strict";
import { test } from "node:test";

import {
  filterComboboxOptions,
  firstEnabledIndex,
  lastEnabledIndex,
  nextEnabledIndex,
  searchText,
  type ComboboxOption,
} from "./combobox-options.ts";

const categories: readonly ComboboxOption[] = [
  { value: "raw", label: "Raw materials" },
  { value: "services", label: "Professional services", description: "Legal, audit and consulting" },
  { value: "cafe", label: "Café and pantry" },
  { value: "freight", label: "Freight", disabled: true },
  { value: "software", label: "Software licences" },
];

test("searchText_AccentsCaseAndOuterSpaces_AreRemoved", () => {
  assert.equal(searchText("  Café  "), "cafe");
  assert.equal(searchText("ÉLAN"), "elan");
});

test("filterComboboxOptions_Query_MatchesLabelsIgnoringCaseAndAccents", () => {
  assert.deepEqual(
    filterComboboxOptions(categories, "CAFE").map((option) => option.value),
    ["cafe"],
  );
  assert.deepEqual(
    filterComboboxOptions(categories, "ic").map((option) => option.value),
    ["services", "software"],
  );
});

test("filterComboboxOptions_QueryInsideAWord_Matches", () => {
  assert.deepEqual(
    filterComboboxOptions(categories, "terial").map((option) => option.value),
    ["raw"],
  );
});

test("filterComboboxOptions_DescriptionOnlyMatch_IsNotAMatch", () => {
  assert.deepEqual(filterComboboxOptions(categories, "audit"), []);
});

test("filterComboboxOptions_BlankQuery_ReturnsEveryOption", () => {
  assert.equal(filterComboboxOptions(categories, ""), categories);
  assert.equal(filterComboboxOptions(categories, "   "), categories);
});

test("filterComboboxOptions_DisabledOption_StillListed", () => {
  assert.deepEqual(
    filterComboboxOptions(categories, "freight").map((option) => option.value),
    ["freight"],
  );
});

test("nextEnabledIndex_Forward_SkipsDisabledAndWraps", () => {
  assert.equal(nextEnabledIndex(categories, 2, 1), 4);
  assert.equal(nextEnabledIndex(categories, 4, 1), 0);
});

test("nextEnabledIndex_Backward_SkipsDisabledAndWraps", () => {
  assert.equal(nextEnabledIndex(categories, 4, -1), 2);
  assert.equal(nextEnabledIndex(categories, 0, -1), 4);
});

test("nextEnabledIndex_NoActiveOption_StartsAtTheMatchingEnd", () => {
  assert.equal(nextEnabledIndex(categories, -1, 1), 0);
  assert.equal(nextEnabledIndex(categories, -1, -1), 4);
  assert.equal(nextEnabledIndex(categories, 99, 1), 0);
});

test("nextEnabledIndex_EveryOptionDisabledOrNone_ReturnsMinusOne", () => {
  const disabled: readonly ComboboxOption[] = [
    { value: "a", label: "A", disabled: true },
    { value: "b", label: "B", disabled: true },
  ];
  assert.equal(nextEnabledIndex(disabled, -1, 1), -1);
  assert.equal(nextEnabledIndex([], -1, 1), -1);
});

const disabledEnds: readonly ComboboxOption[] = [
  { value: "a", label: "A", disabled: true },
  { value: "b", label: "B" },
  { value: "c", label: "C" },
  { value: "d", label: "D", disabled: true },
];

test("firstEnabledIndex_DisabledFirstOption_ReturnsTheFirstEnabledOne", () => {
  assert.equal(firstEnabledIndex(disabledEnds), 1);
});

test("lastEnabledIndex_DisabledLastOption_ReturnsTheLastEnabledOne", () => {
  assert.equal(lastEnabledIndex(disabledEnds), 2);
});
