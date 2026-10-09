export type ShortcutKeyEvent = {
  readonly key?: string | undefined;
  readonly ctrlKey: boolean;
  readonly metaKey: boolean;
  readonly altKey: boolean;
  readonly shiftKey: boolean;
  readonly repeat?: boolean | undefined;
};

const applePlatform = /Mac|iPhone|iPad|iPod/u;

// Both modifiers count everywhere, so a person who uses Ctrl on a Mac or a Mac keyboard elsewhere is not refused; only
// the label follows the platform, which iPadOS reports as Macintosh. A key held down repeats, and only its first press
// counts; a keydown that browser autofill raises carries no key at all.
function shortcut(event: ShortcutKeyEvent, letter: string): boolean {
  return (
    event.repeat !== true &&
    (event.key ?? "").toLowerCase() === letter &&
    (event.ctrlKey || event.metaKey) &&
    !event.altKey &&
    !event.shiftKey
  );
}

export function opensPageSearch(event: ShortcutKeyEvent): boolean {
  return shortcut(event, "k");
}

export function togglesNavigation(event: ShortcutKeyEvent): boolean {
  return event.key === "b" && (event.ctrlKey || event.metaKey);
}

export function pageSearchShortcutLabel(userAgent: string): string {
  return applePlatform.test(userAgent) ? "⌘K" : "Ctrl K";
}
