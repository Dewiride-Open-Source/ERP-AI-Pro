import assert from "node:assert/strict";
import { test } from "node:test";

import {
  chosenOptionLabel,
  filterComboboxOptions,
  firstEnabledIndex,
  lastEnabledIndex,
  nextEnabledIndex,
  rememberChosenOption,
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

const acme: ComboboxOption = { value: "c-42", label: "Acme Ltd" };
const beta: ComboboxOption = { value: "c-7", label: "Beta Traders" };

test("rememberChosenOption_ChosenOptionListed_RemembersIt", () => {
  assert.equal(rememberChosenOption([beta, acme], "c-42", null), acme);
  assert.equal(rememberChosenOption([acme], "c-42", beta), acme);
});

test("rememberChosenOption_ChosenOptionMissingFromASearchResult_KeepsTheRememberedOne", () => {
  const remembered = rememberChosenOption([acme], "c-42", null);
  assert.equal(rememberChosenOption([beta], "c-42", remembered), acme);
  assert.equal(chosenOptionLabel("c-42", rememberChosenOption([], "c-42", remembered)), "Acme Ltd");
});

test("rememberChosenOption_SameValueAndLabelInANewObject_KeepsTheRememberedObject", () => {
  assert.equal(rememberChosenOption([{ value: "c-42", label: "Acme Ltd" }], "c-42", acme), acme);
});

test("rememberChosenOption_ListedLabelChanged_RemembersTheNewLabel", () => {
  const renamed: ComboboxOption = { value: "c-42", label: "Acme Private Limited" };
  assert.equal(rememberChosenOption([renamed], "c-42", acme), renamed);
});

test("rememberChosenOption_NoValue_KeepsTheRememberedOne", () => {
  assert.equal(rememberChosenOption([acme], null, beta), beta);
  assert.equal(rememberChosenOption([acme], null, null), null);
});

test("chosenOptionLabel_RememberedOptionOfAnotherValueOrNoValue_IsEmpty", () => {
  assert.equal(chosenOptionLabel("c-42", acme), "Acme Ltd");
  assert.equal(chosenOptionLabel("c-7", acme), "");
  assert.equal(chosenOptionLabel(null, acme), "");
  assert.equal(chosenOptionLabel("c-42", null), "");
});
