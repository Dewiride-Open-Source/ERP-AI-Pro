import assert from "node:assert/strict";
import { test } from "node:test";
import type { FocusEvent } from "react";

import { leavesGroup } from "./field-focus.ts";

interface FakeElement {
  readonly id: string;
  readonly children: readonly FakeElement[];
  parent: FakeElement | null;
  readonly ownerDocument: { getElementById(id: string): FakeElement | null };
  contains(other: FakeElement | null): boolean;
  getAttribute(name: string): string | null;
  querySelectorAll(selector: string): FakeElement[];
}

interface FakeElementOptions {
  readonly id?: string;
  readonly controls?: string;
}

function descendantsOf(element: FakeElement): FakeElement[] {
  return element.children.flatMap((child) => [child, ...descendantsOf(child)]);
}

function fakeDocument(): (options: FakeElementOptions, ...children: FakeElement[]) => FakeElement {
  const elements: FakeElement[] = [];
  const ownerDocument = {
    getElementById: (id: string) => elements.find((element) => element.id === id) ?? null,
  };
  return (options, ...children) => {
    const element: FakeElement = {
      id: options.id ?? "",
      children,
      parent: null,
      ownerDocument,
      contains(other) {
        for (let node = other; node !== null; node = node.parent) {
          if (node === element) return true;
        }
        return false;
      },
      getAttribute(name) {
        assert.equal(name, "aria-controls");
        return options.controls ?? null;
      },
      querySelectorAll(selector) {
        assert.equal(selector, "[aria-controls]");
        return descendantsOf(element).filter((node) => node.getAttribute("aria-controls") !== null);
      },
    };
    for (const child of children) child.parent = element;
    elements.push(element);
    return element;
  };
}

function datePickerPage({ open, controls = "calendar" }: { open: boolean; controls?: string }) {
  const element = fakeDocument();
  const textBox = element({});
  const trigger = element(open ? { controls } : {});
  const group = element({}, textBox, trigger);
  const yearDropdown = element({});
  const day = element({});
  const nextDay = element({});
  const calendar = element({ id: "calendar" }, element({}, yearDropdown), element({}, day, nextDay));
  const note = element({ id: "note" });
  const nextField = element({});
  element({}, group, nextField, calendar, note);
  return { group, textBox, trigger, yearDropdown, day, nextDay, note, nextField };
}

function blurLeaves(group: FakeElement, target: FakeElement, relatedTarget: FakeElement | null): boolean {
  return leavesGroup({ currentTarget: group, target, relatedTarget } as unknown as FocusEvent<HTMLElement>);
}

test("leavesGroup_FocusMovesFromTheTextBoxToTheTrigger_StaysInTheGroup", () => {
  const page = datePickerPage({ open: false });
  assert.equal(blurLeaves(page.group, page.textBox, page.trigger), false);
});

test("leavesGroup_FocusMovesFromTheTriggerIntoItsOpenCalendar_StaysInTheGroup", () => {
  const page = datePickerPage({ open: true });
  assert.equal(blurLeaves(page.group, page.trigger, page.day), false);
});

test("leavesGroup_FocusMovesFromDayToDayInTheOpenCalendar_StaysInTheGroup", () => {
  const page = datePickerPage({ open: true });
  assert.equal(blurLeaves(page.group, page.day, page.nextDay), false);
});

test("leavesGroup_FocusMovesFromADayToTheYearDropdown_StaysInTheGroup", () => {
  const page = datePickerPage({ open: true });
  assert.equal(blurLeaves(page.group, page.day, page.yearDropdown), false);
});

test("leavesGroup_FocusReturnsFromTheOpenCalendarToTheTrigger_StaysInTheGroup", () => {
  const page = datePickerPage({ open: true });
  assert.equal(blurLeaves(page.group, page.day, page.trigger), false);
});

test("leavesGroup_WindowLosesFocusWhileTheCalendarIsOpen_StaysInTheGroup", () => {
  const page = datePickerPage({ open: true });
  assert.equal(blurLeaves(page.group, page.day, null), false);
});

test("leavesGroup_TriggerNamesSeveralPopups_KeepsFocusInEachOfThemInTheGroup", () => {
  const page = datePickerPage({ open: true, controls: " calendar \t note " });
  assert.equal(blurLeaves(page.group, page.day, page.note), false);
});

test("leavesGroup_FocusMovesFromTheTextBoxToTheNextField_LeavesTheGroup", () => {
  const page = datePickerPage({ open: false });
  assert.equal(blurLeaves(page.group, page.textBox, page.nextField), true);
});

test("leavesGroup_FocusMovesFromTheTextBoxToNowhere_LeavesTheGroup", () => {
  const page = datePickerPage({ open: false });
  assert.equal(blurLeaves(page.group, page.textBox, null), true);
});

test("leavesGroup_FocusMovesFromTheOpenCalendarToTheNextField_LeavesTheGroup", () => {
  const page = datePickerPage({ open: true });
  assert.equal(blurLeaves(page.group, page.day, page.nextField), true);
});

test("leavesGroup_FocusMovesIntoAPopupNoTriggerOfTheFieldControls_LeavesTheGroup", () => {
  const page = datePickerPage({ open: false });
  assert.equal(blurLeaves(page.group, page.textBox, page.day), true);
});
