"use client";

import { MonitorIcon, MoonIcon, SunIcon } from "lucide-react";
import { useTheme } from "next-themes";
import { RadioGroup } from "radix-ui";
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
    <RadioGroup.Root
      value={mounted ? (theme ?? "") : ""}
      onValueChange={setTheme}
      orientation="horizontal"
      aria-label="Colour theme"
      data-testid="theme-toggle"
      className={cn(
        "inline-flex items-center gap-0.5 rounded-full border bg-muted/60 p-0.5 backdrop-blur",
        className,
      )}
    >
      {options.map(({ value, label, Icon }) => (
        <RadioGroup.Item
          key={value}
          value={value}
          aria-label={label}
          title={label}
          data-testid={`theme-${value}`}
          className="inline-flex size-8 items-center justify-center rounded-full text-muted-foreground focus-ring transition-all duration-(--motion-duration-normal) ease-standard hover:text-foreground data-[state=checked]:bg-background data-[state=checked]:text-foreground data-[state=checked]:shadow-sm"
        >
          <Icon className="size-4" aria-hidden />
        </RadioGroup.Item>
      ))}
    </RadioGroup.Root>
  );
}
