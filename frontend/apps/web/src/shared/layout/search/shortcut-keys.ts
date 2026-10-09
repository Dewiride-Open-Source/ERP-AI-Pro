export type ShortcutKeyEvent = {
  readonly key?: string | undefined;
  readonly ctrlKey: boolean;
  readonly metaKey: boolean;
  readonly altKey: boolean;
  readonly shiftKey: boolean;
};

const applePlatform = /Mac|iPhone|iPad|iPod/u;

// Both modifiers count everywhere, so a person who uses Ctrl on a Mac or a Mac keyboard elsewhere is not refused; only
// the label follows the platform, which iPadOS reports as Macintosh. A keydown that browser autofill raises carries no
// key at all.
export function isPageSearchShortcut(event: ShortcutKeyEvent): boolean {
  return (
    (event.key ?? "").toLowerCase() === "k" &&
    (event.ctrlKey || event.metaKey) &&
    !event.altKey &&
    !event.shiftKey
  );
}

export function isNavigationShortcut(event: ShortcutKeyEvent): boolean {
  return event.key === "b" && (event.ctrlKey || event.metaKey);
}

export function pageSearchShortcutLabel(userAgent: string): string {
  return applePlatform.test(userAgent) ? "⌘K" : "Ctrl K";
}
