"use client";

import { useMemo, useSyncExternalStore } from "react";

import { motionTiming, type MotionTiming } from "./motion-tokens";

export type MotionDuration = "fast" | "normal" | "slow";
export type MotionEase = "standard" | "enter";

const reducedMotionQuery = "(prefers-reduced-motion: reduce)";
const separator = "|";
const instant: MotionTiming = { duration: 0, ease: [0, 0, 1, 1] };

function subscribe(onChange: () => void) {
  const query = window.matchMedia(reducedMotionQuery);
  query.addEventListener("change", onChange);
  return () => query.removeEventListener("change", onChange);
}

// The tokens collapse to 0.01ms under prefers-reduced-motion, so reading them at run time keeps motion's JavaScript animations
// on the same durations and the same reduced-motion rule as the CSS ones.
export function useMotionTiming(duration: MotionDuration, ease: MotionEase): MotionTiming {
  const tokens = useSyncExternalStore(
    subscribe,
    () => {
      const style = getComputedStyle(document.documentElement);
      return [
        style.getPropertyValue(`--motion-duration-${duration}`),
        style.getPropertyValue(`--motion-ease-${ease}`),
      ].join(separator);
    },
    () => "",
  );
  return useMemo(() => {
    const [durationValue = "", easeValue = ""] = tokens.split(separator);
    return motionTiming(durationValue, easeValue) ?? instant;
  }, [tokens]);
}
