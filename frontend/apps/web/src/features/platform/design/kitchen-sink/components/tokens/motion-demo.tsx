"use client";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import { Label } from "@dewiride/erp-ui/components/ui/label";
import { Switch } from "@dewiride/erp-ui/components/ui/switch";
import { RotateCcwIcon } from "lucide-react";
import { useState, useSyncExternalStore } from "react";

import type { MotionDurationName } from "./token-catalogue";

const reducedMotionQuery = "(prefers-reduced-motion: reduce)";
const demoDurations: readonly MotionDurationName[] = ["fast", "normal", "slow"];

function subscribeToReducedMotion(onChange: () => void) {
  const query = window.matchMedia(reducedMotionQuery);
  query.addEventListener("change", onChange);
  return () => query.removeEventListener("change", onChange);
}

function readReducedMotion(): boolean | undefined {
  return window.matchMedia(reducedMotionQuery).matches;
}

function readNothing(): undefined {
  return undefined;
}

function useReducedMotion() {
  return useSyncExternalStore(subscribeToReducedMotion, readReducedMotion, readNothing);
}

function useMotionDuration(name: MotionDurationName) {
  return useSyncExternalStore(
    subscribeToReducedMotion,
    () => getComputedStyle(document.documentElement).getPropertyValue(`--motion-duration-${name}`).trim(),
    () => "",
  );
}

export function MotionDemo() {
  const reducedMotion = useReducedMotion();
  const [replays, setReplays] = useState(0);
  const [moved, setMoved] = useState(false);

  return (
    <div className="grid min-w-0 gap-4 rounded-xl border bg-card p-4">
      <div className="grid gap-1">
        <h3 className="text-body font-medium">Live demo</h3>
        <p
          data-testid="motion-demo-preference"
          data-reduced-motion={reducedMotion === undefined ? undefined : String(reducedMotion)}
          className="text-caption text-muted-foreground"
        >
          {describePreference(reducedMotion)}
        </p>
      </div>

      <dl className="grid grid-cols-3 gap-3">
        {demoDurations.map((name) => (
          <DurationReading key={name} name={name} />
        ))}
      </dl>

      <div className="grid gap-3 sm:grid-cols-2">
        <div className="grid content-start gap-3">
          <Button
            type="button"
            variant="outline"
            className="w-fit"
            onClick={() => setReplays((count) => count + 1)}
          >
            <RotateCcwIcon data-icon="inline-start" aria-hidden />
            Replay entrance
          </Button>
          <div
            key={replays}
            data-testid="motion-demo-animated"
            className="animate-fade-up rounded-lg border bg-muted/40 p-4 text-body"
          >
            Fades up over the slow duration with the enter curve.
          </div>
        </div>

        <div className="grid content-start gap-3">
          <div className="flex items-center gap-2">
            <Switch id="motion-demo-transition-toggle" checked={moved} onCheckedChange={setMoved} />
            <Label htmlFor="motion-demo-transition-toggle">Slide the sample</Label>
          </div>
          <div className="overflow-hidden rounded-lg border bg-muted/40 p-1">
            <div
              data-testid="motion-demo-transitioned"
              data-state={moved ? "on" : "off"}
              className={`h-8 w-1/2 rounded-md bg-primary transition-transform duration-(--motion-duration-normal) ease-standard ${moved ? "translate-x-full" : "translate-x-0"}`}
            />
          </div>
        </div>
      </div>
    </div>
  );
}

function describePreference(reducedMotion: boolean | undefined) {
  if (reducedMotion === undefined) return "Reading the reduced-motion preference.";
  return reducedMotion
    ? "Reduced motion is requested: every duration collapses to 0.01ms."
    : "Reduced motion is not requested: the token durations apply.";
}

function formatAsMilliseconds(value: string) {
  const match = /^(\d*\.?\d+)(ms|s)$/.exec(value);
  const amount = match?.[1];
  const unit = match?.[2];
  if (amount === undefined || unit === undefined) return value;
  const milliseconds = Number(amount) * (unit === "s" ? 1000 : 1);
  return `${Number(milliseconds.toFixed(3))}ms`;
}

function DurationReading({ name }: { name: MotionDurationName }) {
  const value = useMotionDuration(name);

  return (
    <div className="grid min-w-0 gap-0.5 rounded-lg border p-3">
      <dt className="text-caption text-muted-foreground">{name}</dt>
      <dd
        data-testid={`motion-demo-duration-${name}`}
        data-computed={value || undefined}
        className="truncate font-mono text-body"
      >
        {value ? formatAsMilliseconds(value) : "—"}
      </dd>
    </div>
  );
}
