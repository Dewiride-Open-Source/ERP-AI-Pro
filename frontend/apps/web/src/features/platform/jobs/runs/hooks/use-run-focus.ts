import { useCallback, useLayoutEffect, useRef } from "react";

function shown(selector: string): HTMLElement | undefined {
  return [...document.querySelectorAll<HTMLElement>(selector)].find(
    (element) => element.getClientRects().length > 0,
  );
}

// A run's button leaves before the rows its action refreshed are drawn, and the list draws each run twice, as a row and as
// a card, of which it shows one; so once the refreshed rows are in, focus goes to the shown copy of the run's first button,
// else of its link, and to the heading when the run has left the page.
export function useRunFocus(rows: unknown, headingId: string): (runId: string) => void {
  const leftRun = useRef<string | undefined>(undefined);

  useLayoutEffect(() => {
    const runId = leftRun.current;
    if (runId === undefined) return;
    leftRun.current = undefined;
    const id = CSS.escape(runId);
    const target =
      shown(`[data-run-actions="${id}"] button`) ??
      shown(`[data-run-link="${id}"]`) ??
      document.getElementById(headingId);
    target?.focus();
  }, [rows, headingId]);

  return useCallback((runId: string) => {
    leftRun.current = runId;
  }, []);
}
