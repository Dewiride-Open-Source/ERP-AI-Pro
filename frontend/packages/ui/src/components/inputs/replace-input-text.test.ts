import assert from "node:assert/strict";
import { test } from "node:test";

import { normaliseIdentifier } from "../../lib/identifiers.ts";
import { sanitiseAmountText } from "../../lib/indian-number.ts";

import { replaceInputText, type EditableText } from "./replace-input-text.ts";

class FakeInput implements EditableText {
  value: string;
  selectionEnd: number | null;
  selection: [number, number] | undefined;

  constructor(value: string, caret: number | null) {
    this.value = value;
    this.selectionEnd = caret;
  }

  setSelectionRange(start: number, end: number): void {
    this.selection = [start, end];
  }
}

const identifier = (text: string) => normaliseIdentifier(text, 15);
const rupees = (text: string) => sanitiseAmountText(text, { scale: 2, allowNegative: false });

test("replaceInputText_TextAlreadyClean_LeavesTheInputAlone", () => {
  const input = new FakeInput("27ABC", 5);
  assert.equal(replaceInputText(input, identifier), "27ABC");
  assert.equal(input.value, "27ABC");
  assert.equal(input.selection, undefined);
});

test("replaceInputText_CharacterRemovedBeforeTheCaret_MovesTheCaretBack", () => {
  const input = new FakeInput("12a3", 3);
  assert.equal(replaceInputText(input, rupees), "123");
  assert.equal(input.value, "123");
  assert.deepEqual(input.selection, [2, 2]);
});

test("replaceInputText_LowerCaseTypedInTheMiddle_KeepsTheCaretAfterIt", () => {
  const input = new FakeInput("27Abcde", 3);
  assert.equal(replaceInputText(input, identifier), "27ABCDE");
  assert.deepEqual(input.selection, [3, 3]);
});

test("replaceInputText_PastedTextWithSpaces_PutsTheCaretAfterThePaste", () => {
  const input = new FakeInput("27 abcde 1234f 1z5", 18);
  assert.equal(replaceInputText(input, identifier), "27ABCDE1234F1Z5");
  assert.deepEqual(input.selection, [15, 15]);
});

test("replaceInputText_UnknownCaret_PutsTheCaretAtTheEnd", () => {
  const input = new FakeInput("1,500", null);
  assert.equal(replaceInputText(input, rupees), "1500");
  assert.deepEqual(input.selection, [4, 4]);
});
