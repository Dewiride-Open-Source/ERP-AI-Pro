import assert from "node:assert/strict";
import { test } from "node:test";

import type { FormControlProps } from "@dewiride/erp-ui/components/forms/form-field";

import { rangeEndState } from "./range-end-state.ts";

function frame(describedBy: string | undefined): FormControlProps {
  return {
    id: "supplier-validity-from",
    "aria-invalid": undefined,
    "aria-describedby": describedBy,
    "aria-required": true,
  };
}

test("rangeEndState_EndWithItsOwnMessage_IsInvalidAndDescribedByTheErrorRegion", () => {
  assert.deepEqual(
    rangeEndState(frame("supplier-validity-from-description supplier-validity-from-error"), [
      "Enter the end date.",
    ]),
    {
      invalid: true,
      describedBy: "supplier-validity-from-description supplier-validity-from-error",
    },
  );
});

test("rangeEndState_OnlyTheOtherEndHasAMessage_IsValidAndDescribedByTheDescriptionAlone", () => {
  assert.deepEqual(
    rangeEndState(frame("supplier-validity-from-description supplier-validity-from-error"), undefined),
    { invalid: false, describedBy: "supplier-validity-from-description" },
  );
});

test("rangeEndState_EmptyMessageList_IsValid", () => {
  assert.deepEqual(rangeEndState(frame("supplier-validity-from-description"), []), {
    invalid: false,
    describedBy: "supplier-validity-from-description",
  });
});

test("rangeEndState_OtherEndInvalidAndNoDescription_IsDescribedByNothing", () => {
  assert.deepEqual(rangeEndState(frame("supplier-validity-from-error"), undefined), {
    invalid: false,
    describedBy: undefined,
  });
});

test("rangeEndState_NoDescriptionAndNoMessage_IsDescribedByNothing", () => {
  assert.deepEqual(rangeEndState(frame(undefined), undefined), { invalid: false, describedBy: undefined });
});
