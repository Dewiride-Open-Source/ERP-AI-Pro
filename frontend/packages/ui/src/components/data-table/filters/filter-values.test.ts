import assert from "node:assert/strict";
import { test } from "node:test";

import {
  dateRangeProblems,
  optionsSummary,
  submittedDate,
  toggleOption,
  type DataTableFilterOption,
} from "./filter-values.ts";

const statuses: readonly DataTableFilterOption[] = [
  { value: "draft", label: "Draft" },
  { value: "approved", label: "Approved" },
  { value: "paid", label: "Paid" },
];

test("toggleOption_CheckedAndUnchecked_KeepsTheOptionsOrder", () => {
  assert.deepEqual(toggleOption(["paid"], "draft", true, statuses), ["draft", "paid"]);
  assert.deepEqual(toggleOption(["draft", "paid"], "draft", false, statuses), ["paid"]);
  assert.deepEqual(toggleOption(["paid"], "paid", true, statuses), ["paid"]);
});

test("toggleOption_ValueNotAmongTheOptions_IsDropped", () => {
  assert.deepEqual(toggleOption(["cancelled"], "draft", true, statuses), ["draft"]);
});

test("optionsSummary_NoneOneOrMany_NamesTheChoice", () => {
  assert.equal(optionsSummary([], statuses), "Any");
  assert.equal(optionsSummary(["approved"], statuses), "Approved");
  assert.equal(optionsSummary(["draft", "paid"], statuses), "2 selected");
});

test("dateRangeProblems_EmptyOrOpenEndedRange_HasNoProblem", () => {
  assert.deepEqual(dateRangeProblems({ from: "", to: "" }), { from: undefined, to: undefined });
  assert.deepEqual(dateRangeProblems({ from: "2026-04-01", to: "" }), { from: undefined, to: undefined });
  assert.deepEqual(dateRangeProblems({ from: "", to: "2026-03-31" }), { from: undefined, to: undefined });
});

test("dateRangeProblems_TextThatIsNotADate_NamesTheEnd", () => {
  const problems = dateRangeProblems({ from: "31-02-2026", to: "2026-03-31" });
  assert.match(problems.from ?? "", /^Enter a real date/);
  assert.equal(problems.to, undefined);
});

test("dateRangeProblems_EndBeforeStart_IsReportedOnTheEnd", () => {
  assert.deepEqual(dateRangeProblems({ from: "2026-04-02", to: "2026-04-01" }), {
    from: undefined,
    to: "The end date is before the start date.",
  });
  assert.deepEqual(dateRangeProblems({ from: "2026-04-01", to: "2026-04-01" }), {
    from: undefined,
    to: undefined,
  });
});

test("submittedDate_OnlyACalendarDate_IsSent", () => {
  assert.equal(submittedDate("2026-04-01"), "2026-04-01");
  assert.equal(submittedDate("01-04-2026"), "");
  assert.equal(submittedDate(""), "");
});
