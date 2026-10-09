// Where the pointer rested when a list last chose its active option without it: "unseen" until a mousemove reports the
// position, "moving" once the pointer has left that position.
export type PointerRest = "unseen" | "moving" | { readonly x: number; readonly y: number };

// A browser may raise mousemove at a pointer that has not moved, when the content under it scrolls or changes (WebKit
// does once a list has scrolled its active option into view), so the pointer chooses the active option only after it
// has really moved since the list opened or the keyboard last chose one.
export function followPointer(
  rest: PointerRest,
  x: number,
  y: number,
): { readonly follows: boolean; readonly rest: PointerRest } {
  if (rest === "moving") return { follows: true, rest };
  if (rest === "unseen") return { follows: false, rest: { x, y } };
  return rest.x === x && rest.y === y ? { follows: false, rest } : { follows: true, rest: "moving" };
}
