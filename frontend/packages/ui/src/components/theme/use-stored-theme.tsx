"use client";

import { useSyncExternalStore } from "react";

import { resolveStoredTheme, themeStorageKey, type ResolvedTheme } from "./stored-theme";

const darkQuery = "(prefers-color-scheme: dark)";

function subscribe(onChange: () => void) {
  const query = window.matchMedia(darkQuery);
  query.addEventListener("change", onChange);
  window.addEventListener("storage", onChange);
  return () => {
    query.removeEventListener("change", onChange);
    window.removeEventListener("storage", onChange);
  };
}

function readStoredTheme(): string | null {
  // Reading storage throws when the browser blocks this site's data, and the system preference still applies then.
  try {
    return window.localStorage.getItem(themeStorageKey);
  } catch {
    return null;
  }
}

function lightTheme(): ResolvedTheme {
  return "light";
}

export function useStoredTheme(): ResolvedTheme {
  return useSyncExternalStore(
    subscribe,
    () => resolveStoredTheme(readStoredTheme(), window.matchMedia(darkQuery).matches),
    lightTheme,
  );
}
