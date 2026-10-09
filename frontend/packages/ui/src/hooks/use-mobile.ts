import * as React from "react";

const mobileQuery = "(width < 48rem)";

function subscribe(onChange: () => void) {
  const mql = window.matchMedia(mobileQuery);
  mql.addEventListener("change", onChange);
  return () => mql.removeEventListener("change", onChange);
}

export function useIsMobile() {
  return React.useSyncExternalStore(
    subscribe,
    () => window.matchMedia(mobileQuery).matches,
    () => false,
  );
}
