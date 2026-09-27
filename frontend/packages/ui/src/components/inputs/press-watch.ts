export interface PressWatch {
  readonly pending: boolean;
  start(document: EventTarget, onEnd: () => void): void;
  cancel(): void;
}

// A context menu that opens on the press, as Ctrl+click does on macOS, takes the release, so no mouseup reaches the
// page and the menu ends the press instead.
const pressEndEvents = ["mouseup", "contextmenu"] as const;

export function createPressWatch(): PressWatch {
  let watch: AbortController | null = null;

  const cancel = () => {
    watch?.abort();
    watch = null;
  };

  return {
    get pending() {
      return watch !== null;
    },
    start(document, onEnd) {
      cancel();
      const current = new AbortController();
      watch = current;
      const end = () => {
        cancel();
        onEnd();
      };
      for (const type of pressEndEvents) {
        document.addEventListener(type, end, { capture: true, signal: current.signal });
      }
    },
    cancel,
  };
}
