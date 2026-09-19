"use client";

import { MonitorIcon, MoonIcon, SunIcon } from "lucide-react";
import { useTheme } from "next-themes";
import { useSyncExternalStore } from "react";

import { cn } from "@dewiride/erp-ui/lib/utils";

const options = [
  { value: "light", label: "Light", Icon: SunIcon },
  { value: "dark", label: "Dark", Icon: MoonIcon },
  { value: "system", label: "System", Icon: MonitorIcon },
] as const;

const subscribeToNothing = () => () => {};

export function ThemeToggle({ className }: { className?: string }) {
  const { theme, setTheme } = useTheme();
  const mounted = useSyncExternalStore(
    subscribeToNothing,
    () => true,
    () => false,
  );

  return (
    <div
      role="radiogroup"
      aria-label="Colour theme"
      data-testid="theme-toggle"
      className={cn(
        "inline-flex items-center gap-0.5 rounded-full border bg-muted/60 p-0.5 backdrop-blur",
        className,
      )}
    >
      {options.map(({ value, label, Icon }) => {
        const active = mounted && theme === value;
        return (
          <button
            key={value}
            type="button"
            role="radio"
            aria-checked={active}
            aria-label={label}
            title={label}
            data-testid={`theme-${value}`}
            onClick={() => setTheme(value)}
            className={cn(
              "inline-flex size-8 items-center justify-center rounded-full transition-all duration-200 ease-out focus-visible:ring-2 focus-visible:ring-ring/60 focus-visible:outline-none",
              active
                ? "bg-background text-foreground shadow-sm"
                : "text-muted-foreground hover:text-foreground",
            )}
          >
            <Icon className="size-4" aria-hidden />
          </button>
        );
      })}
    </div>
  );
}
