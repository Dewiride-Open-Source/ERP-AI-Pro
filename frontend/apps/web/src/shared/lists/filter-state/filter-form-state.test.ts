import assert from "node:assert/strict";
import { test } from "node:test";

import {
  filterFormStateFor,
  filtersApplied,
  filtersClearing,
  initialFilterFormState,
} from "./filter-form-state.ts";

const unfiltered = "";

const kaveri = "?supplier=Kaveri";

const deccan = "?supplier=Deccan";

test("initialFilterFormState_Filters_StartsTheFirstFormWithoutFocusingIt", () => {
  assert.deepEqual(initialFilterFormState(kaveri), {
    signature: kaveri,
    key: 0,
    applied: undefined,
    clearing: false,
    focusFirstField: false,
  });
});

test("filterFormStateFor_SameFilters_KeepsTheState", () => {
  const state = filtersApplied(initialFilterFormState(kaveri), deccan);

  assert.equal(filterFormStateFor(state, kaveri), state);
});

test("filterFormStateFor_FiltersTheFormApplied_KeepsTheFormMounted", () => {
  const applied = filtersApplied(initialFilterFormState(unfiltered), kaveri);

  assert.deepEqual(filterFormStateFor(applied, kaveri), {
    signature: kaveri,
    key: 0,
    applied: undefined,
    clearing: false,
    focusFirstField: false,
  });
});

test("filterFormStateFor_FiltersChangedElsewhere_RemountsTheForm", () => {
  const next = filterFormStateFor(initialFilterFormState(kaveri), deccan);

  assert.equal(next.key, 1);
  assert.equal(next.signature, deccan);
  assert.equal(next.focusFirstField, false);
});

test("filterFormStateFor_FiltersOtherThanTheFormApplied_RemountsTheForm", () => {
  const applied = filtersApplied(initialFilterFormState(unfiltered), kaveri);

  assert.equal(filterFormStateFor(applied, deccan).key, 1);
});

test("filterFormStateFor_AfterClearFilters_RemountsTheFormAndFocusesItsFirstField", () => {
  const clearing = filtersClearing(initialFilterFormState(kaveri));

  assert.deepEqual(filterFormStateFor(clearing, unfiltered), {
    signature: unfiltered,
    key: 1,
    applied: undefined,
    clearing: false,
    focusFirstField: true,
  });
});

test("filterFormStateFor_ChangeAfterAClear_LeavesTheFocusWhereItIs", () => {
  const cleared = filterFormStateFor(filtersClearing(initialFilterFormState(kaveri)), unfiltered);

  const next = filterFormStateFor(cleared, deccan);

  assert.equal(next.key, 2);
  assert.equal(next.focusFirstField, false);
});
