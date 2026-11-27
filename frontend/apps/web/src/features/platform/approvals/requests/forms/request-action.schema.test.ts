import assert from "node:assert/strict";
import { test } from "node:test";

import {
  commentMaxLength,
  requestActionSchema,
  requestActionValues,
  requiresReason,
  type RequestAction,
} from "./request-action.schema.ts";

const id = "019a8f2e-7c41-7b3d-9e5f-0a1b2c3d4e5f";

const version = "AAAAAAAAB9E=";

const actions: readonly RequestAction[] = ["approve", "return", "reject", "withdraw"];

function messages(action: RequestAction, values: Record<string, unknown>): string[] {
  const parsed = requestActionSchema(action).safeParse(values);
  return parsed.success
    ? []
    : parsed.error.issues.map((issue) => `${issue.path.join(".")}: ${issue.message}`);
}

test("requestActionSchema_SendingBackWithoutAReason_AsksWhatNeedsToChange", () => {
  assert.deepEqual(messages("return", { id, version, comment: "   " }), [
    "comment: Say what needs to change.",
  ]);
});

test("requestActionSchema_RejectingWithoutAReason_AsksWhy", () => {
  assert.deepEqual(messages("reject", { id, version, comment: "" }), ["comment: Say why it is rejected."]);
});

test("requestActionSchema_ApprovingOrWithdrawingWithoutAComment_SendsNone", () => {
  for (const action of ["approve", "withdraw"] as const) {
    const parsed = requestActionSchema(action).parse({ id, version, comment: "  " });

    assert.deepEqual(parsed, { id, version, comment: null });
  }
});

test("requestActionSchema_AReason_IsKeptWithoutTheSpacesAtEitherEnd", () => {
  const parsed = requestActionSchema("return").parse({
    id,
    version,
    comment: "  Attach the vendor's invoice.  ",
  });

  assert.deepEqual(parsed, { id, version, comment: "Attach the vendor's invoice." });
});

test("requestActionSchema_TheLongestComment_IsAccepted", () => {
  for (const action of actions) {
    assert.deepEqual(messages(action, { id, version, comment: "x".repeat(commentMaxLength) }), [], action);
  }
});

test("requestActionSchema_ACommentOneCharacterTooLong_IsRefused", () => {
  for (const action of actions) {
    assert.deepEqual(
      messages(action, { id, version, comment: "x".repeat(commentMaxLength + 1) }),
      ["comment: Use 1,000 characters or fewer."],
      action,
    );
  }
});

test("requestActionSchema_ARequestThePageDidNotRead_IsOutOfDate", () => {
  const outOfDate = "This page is out of date. Reload the page, then send the form again.";

  assert.deepEqual(messages("approve", { id: "not-an-id", version, comment: "" }), [`id: ${outOfDate}`]);
  assert.deepEqual(messages("approve", { id, version: "", comment: "" }), [`version: ${outOfDate}`]);
  assert.deepEqual(messages("approve", { id, version: "x".repeat(65), comment: "" }), [
    `version: ${outOfDate}`,
  ]);
});

test("requestActionValues_ARequest_StartsWithoutAComment", () => {
  assert.deepEqual(requestActionValues(id, version), { id, version, comment: "" });
});

test("requiresReason_EachAction_SaysWhetherItNeedsAReason", () => {
  assert.deepEqual(
    actions.map((action) => requiresReason(action)),
    [false, true, true, false],
  );
});
