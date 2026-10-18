"use client";

import { useThemeChoice } from "@dewiride/erp-ui/components/theme/use-theme-choice";
import { Label } from "@dewiride/erp-ui/components/ui/label";
import { RadioGroup, RadioGroupItem } from "@dewiride/erp-ui/components/ui/radio-group";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";

const options = [
  { value: "system", label: "System", hint: "Light or dark, as this device is set." },
  { value: "light", label: "Light", hint: "Dark text on a light background." },
  { value: "dark", label: "Dark", hint: "Light text on a dark background." },
] as const;

// The browser holds the theme it shows, so the choice appears only once the page is interactive; choosing changes the
// theme at once, and the shell's theme keeper saves it to the person's account.
export function ThemeChooser({ labelledBy }: { labelledBy: string }) {
  const { choice, choose } = useThemeChoice();
  const hydrated = useHydrated();

  return (
    <RadioGroup
      aria-labelledby={labelledBy}
      value={hydrated ? (choice ?? "") : ""}
      onValueChange={(value) => {
        const option = options.find((candidate) => candidate.value === value);
        if (option !== undefined) choose(option.value);
      }}
      disabled={!hydrated}
      data-hydrating={hydrated ? undefined : ""}
      data-testid="profile-theme-chooser"
      className="gap-4"
    >
      {options.map(({ value, label, hint }) => (
        <div key={value} className="flex items-start gap-3">
          <RadioGroupItem
            id={`profile-theme-${value}`}
            value={value}
            aria-describedby={`profile-theme-${value}-hint`}
            className="mt-0.5"
          />
          <div className="grid gap-1">
            <Label htmlFor={`profile-theme-${value}`}>{label}</Label>
            <p id={`profile-theme-${value}-hint`} className="text-sm text-muted-foreground">
              {hint}
            </p>
          </div>
        </div>
      ))}
    </RadioGroup>
  );
}
