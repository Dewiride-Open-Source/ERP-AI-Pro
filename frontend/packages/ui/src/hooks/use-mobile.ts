import * as React from "react";

const mobileQuery = "(width < 48rem)";

function subscribe(onChange: () => void) {
  const mql = window.matchMedia(mobileQuery);
  mql.addEventListener("change", onChange);
  return () => mql.removeEventListener("change", onChange);
}

export function matchesMobileLayout() {
  return window.matchMedia(mobileQuery).matches;
}

export function useIsMobile() {
  return React.useSyncExternalStore(subscribe, matchesMobileLayout, () => false);
}
