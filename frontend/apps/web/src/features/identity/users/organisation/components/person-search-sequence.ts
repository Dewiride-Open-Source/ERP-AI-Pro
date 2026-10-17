export interface PauseTimer<THandle> {
  readonly set: (run: () => void, delay: number) => THandle;
  readonly clear: (handle: THandle | undefined) => void;
}

export interface PersonSearchSequence {
  readonly afterPause: (send: () => void) => void;
  readonly ticket: () => number;
  readonly isLatest: (ticket: number) => boolean;
  readonly started: () => boolean;
  readonly stop: () => void;
}

export const typingPause = 250;

// Next.js sends a page's Server Function calls one at a time, so a search waits until typing pauses, and an answer that
// arrives after a later search was sent is dropped, so the options always match the text in the field.
export function personSearchSequence<THandle>(
  timer: PauseTimer<THandle>,
  pause: number = typingPause,
): PersonSearchSequence {
  let latest = 0;
  let waiting: THandle | undefined;

  return {
    afterPause: (send) => {
      timer.clear(waiting);
      waiting = timer.set(() => {
        waiting = undefined;
        send();
      }, pause);
    },
    ticket: () => {
      latest += 1;
      return latest;
    },
    isLatest: (ticket) => ticket === latest,
    started: () => latest > 0 || waiting !== undefined,
    stop: () => {
      timer.clear(waiting);
      waiting = undefined;
    },
  };
}
