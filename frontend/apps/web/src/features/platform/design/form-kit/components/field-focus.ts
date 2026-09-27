import type { FocusEvent } from "react";

// A field made of several focusable parts and of the popups its own triggers control counts as visited only when focus
// leaves all of them, so moving to the next part, into the calendar or around inside it never shows an error before the
// person is done. A calendar is portalled outside the field's element, yet React still bubbles its focus events to the
// field, and an open trigger names it with aria-controls. A blur from inside an open popup that moves focus nowhere is the
// window losing focus, not the person leaving the field.
export function leavesGroup(event: FocusEvent<HTMLElement>): boolean {
  const group = event.currentTarget;
  const popups = controlledPopups(group);
  const next = event.relatedTarget;
  if (next === null) return !popups.some((popup) => popup.contains(event.target));
  return !group.contains(next) && !popups.some((popup) => popup.contains(next));
}

function controlledPopups(group: HTMLElement): HTMLElement[] {
  return Array.from(
    group.querySelectorAll("[aria-controls]"),
    (trigger) => trigger.getAttribute("aria-controls") ?? "",
  )
    .flatMap((ids) => ids.split(/\s+/))
    .filter((id) => id !== "")
    .map((id) => group.ownerDocument.getElementById(id))
    .filter((popup) => popup !== null);
}
