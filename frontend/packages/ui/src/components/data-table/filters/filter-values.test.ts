import assert from "node:assert/strict";
import { test } from "node:test";

import {
  dateRangeProblems,
  optionsSummary,
  submittedDate,
  toggleOption,
  trimmedPaste,
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

const id = "0192f3c4-5b6a-7d8e-9f01-23456789abcd";

test("trimmedPaste_TextWithoutSpaceAroundIt_IsLeftToTheBrowser", () => {
  assert.equal(trimmedPaste("", { start: 0, end: 0 }, id, 36), undefined);
  assert.equal(trimmedPaste("", { start: 0, end: 0 }, "a b", 36), undefined);
  assert.equal(trimmedPaste("abc", { start: 3, end: 3 }, "", 36), undefined);
});

test("trimmedPaste_IdCopiedWithSpaceAroundIt_EntersWithoutTheSpace", () => {
  assert.deepEqual(trimmedPaste("", { start: 0, end: 0 }, ` ${id}`, 36), { value: id, caret: 36 });
  assert.deepEqual(trimmedPaste("", { start: 0, end: 0 }, `\t${id}\r\n`, 36), { value: id, caret: 36 });
  assert.deepEqual(trimmedPaste("", { start: 0, end: 0 }, " a b ", 36), { value: "a b", caret: 3 });
});

test("trimmedPaste_LinesCopiedWithSpaceAroundThem_EnterAsOneLineWithASpaceForEachBreak", () => {
  assert.deepEqual(trimmedPaste("abcdef", { start: 3, end: 3 }, " x\ny ", 36), {
    value: "abcx ydef",
    caret: 6,
  });
  assert.deepEqual(trimmedPaste("", { start: 0, end: 0 }, "Payroll\r\nOperations\n", 36), {
    value: "Payroll Operations",
    caret: 18,
  });
  assert.deepEqual(trimmedPaste("", { start: 0, end: 0 }, " a\nbcde ", 5), { value: "a bcd", caret: 5 });
});

test("trimmedPaste_OverASelection_ReplacesItAndPutsTheCaretAfterThePastedText", () => {
  assert.deepEqual(trimmedPaste("not-an-id", { start: 0, end: 9 }, ` ${id}`, 36), { value: id, caret: 36 });
  assert.deepEqual(trimmedPaste("abcdef", { start: 2, end: 4 }, " xy ", 36), { value: "abxyef", caret: 4 });
});

test("trimmedPaste_MoreThanTheFieldHoldsAfterTrimming_IsCutAtTheLimit", () => {
  assert.deepEqual(trimmedPaste("abc", { start: 3, end: 3 }, " 12345 ", 5), { value: "abc12", caret: 5 });
  assert.deepEqual(trimmedPaste("abcde", { start: 2, end: 2 }, " 1 ", 5), { value: "abcde", caret: 2 });
});

test("trimmedPaste_LimitInsideACharacterOfTwoCodeUnits_LeavesThatCharacterOut", () => {
  assert.deepEqual(trimmedPaste("ab", { start: 2, end: 2 }, " c\u{1F600} ", 4), { value: "abc", caret: 3 });
});

test("trimmedPaste_OnlySpace_LeavesNothingButRemovesTheSelection", () => {
  assert.deepEqual(trimmedPaste("abc", { start: 3, end: 3 }, "  ", 36), { value: "abc", caret: 3 });
  assert.deepEqual(trimmedPaste("abc", { start: 1, end: 2 }, "\n", 36), { value: "ac", caret: 1 });
});
