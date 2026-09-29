import { useSyncExternalStore } from "react";

function subscribeToNothing(): () => void {
  return () => undefined;
}

function hydrated(): boolean {
  return true;
}

function notHydrated(): boolean {
  return false;
}

// Controls that only work through React (sorting, the column menu, row selection, the page size) render disabled in the
// server's HTML and come alive when the page hydrates, so a click before then is never silently lost.
export function useHydrated(): boolean {
  return useSyncExternalStore(subscribeToNothing, hydrated, notHydrated);
}
