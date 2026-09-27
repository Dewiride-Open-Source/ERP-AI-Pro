"use client";

import { useState, type ChangeEvent, type ComponentProps, type FocusEvent } from "react";

import { InputGroupInput } from "@dewiride/erp-ui/components/ui/input-group";
import { formatDisplayDate, toCanonicalDate } from "@dewiride/erp-ui/lib/calendar-date";

export type DateTextInputProps = Omit<
  ComponentProps<"input">,
  "value" | "defaultValue" | "onChange" | "type" | "min" | "max"
> & {
  value: string;
  onValueChange: (value: string) => void;
};

export function DateTextInput({
  value,
  onValueChange,
  placeholder = "dd-mm-yyyy",
  onFocus,
  onBlur,
  ...inputProps
}: DateTextInputProps) {
  const [draft, setDraft] = useState<string | null>(null);
  const shown = draft !== null && toCanonicalDate(draft) === value ? draft : formatDisplayDate(value);

  const onChange = (event: ChangeEvent<HTMLInputElement>) => {
    const text = event.currentTarget.value;
    setDraft(text);
    const canonical = toCanonicalDate(text);
    if (canonical !== value) onValueChange(canonical);
  };

  const handleFocus = (event: FocusEvent<HTMLInputElement>) => {
    setDraft(formatDisplayDate(value));
    onFocus?.(event);
  };

  const handleBlur = (event: FocusEvent<HTMLInputElement>) => {
    setDraft(null);
    onBlur?.(event);
  };

  return (
    <InputGroupInput
      {...inputProps}
      type="text"
      autoComplete="off"
      spellCheck={false}
      placeholder={placeholder}
      value={shown}
      onChange={onChange}
      onFocus={handleFocus}
      onBlur={handleBlur}
    />
  );
}
