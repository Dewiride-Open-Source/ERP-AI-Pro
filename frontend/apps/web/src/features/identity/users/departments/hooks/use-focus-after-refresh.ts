import { useCallback, useLayoutEffect, useRef } from "react";

type PendingFocus = { readonly id: string; readonly data: unknown };

// A save refreshes the page, and the refreshed render draws an element it places elsewhere, such as a department moved
// under another one, as a new element. The dialog can hand focus back to the old element before that render arrives, so
// the element with the id takes focus again once the refreshed data is in, unless focus has gone elsewhere meanwhile.
export function useFocusAfterRefresh(data: unknown, settled: boolean): (id: string) => void {
  const pending = useRef<PendingFocus | undefined>(undefined);

  useLayoutEffect(() => {
    const focus = pending.current;
    if (focus === undefined || !settled) return;
    if (document.activeElement === null || document.activeElement === document.body) {
      document.getElementById(focus.id)?.focus();
    }
    if (data !== focus.data) pending.current = undefined;
  }, [data, settled]);

  return useCallback(
    (id: string) => {
      pending.current = { id, data };
    },
    [data],
  );
}
