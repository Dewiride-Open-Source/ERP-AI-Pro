import type { FocusEvent } from "react";

// A field made of several focusable parts, or with a calendar its trigger controls, counts as visited only when focus
// leaves all of them, so moving on to the next part or into the calendar never shows an error before the person is done.
export function leavesGroup(event: FocusEvent<HTMLElement>): boolean {
  const next = event.relatedTarget;
  if (!(next instanceof Element)) return true;
  if (event.currentTarget.contains(next)) return false;
  const controlled = event.target.getAttribute("aria-controls");
  return controlled === null || next.closest(`[id="${CSS.escape(controlled)}"]`) === null;
}
