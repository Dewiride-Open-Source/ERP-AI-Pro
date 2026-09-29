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

// Controls that only work through React render disabled in the server's HTML and come alive when their component hydrates,
// so a click before then is never silently lost. A component that renders such a server-only state marks its element with
// data-hydrating until it hydrates, because nothing else on the page tells a reader when that state has ended.
export function useHydrated(): boolean {
  return useSyncExternalStore(subscribeToNothing, hydrated, notHydrated);
}
