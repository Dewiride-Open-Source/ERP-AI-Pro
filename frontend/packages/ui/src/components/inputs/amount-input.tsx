"use client";

import { useState, type ChangeEvent, type ComponentProps, type FocusEvent } from "react";

import {
  InputGroup,
  InputGroupAddon,
  InputGroupInput,
  InputGroupText,
} from "@dewiride/erp-ui/components/ui/input-group";
import {
  groupIndian,
  normaliseAmount,
  sanitiseAmountText,
  ungroupedOffset,
  type AmountFormat,
} from "@dewiride/erp-ui/lib/indian-number";

import { replaceInputText } from "./replace-input-text";

export type AmountInputProps = Omit<
  ComponentProps<"input">,
  "value" | "defaultValue" | "onChange" | "type" | "inputMode"
> & {
  value: string;
  onValueChange: (value: string) => void;
  scale?: number | undefined;
  allowNegative?: boolean | undefined;
};

function canonicalOf(text: string, format: AmountFormat): string {
  const normalised = normaliseAmount(text, format);
  return "canonical" in normalised ? normalised.canonical : text;
}

export function AmountInput({
  value,
  onValueChange,
  scale = 2,
  allowNegative = false,
  className,
  onFocus,
  onBlur,
  ...inputProps
}: AmountInputProps) {
  const format: AmountFormat = { scale, allowNegative };
  const [draft, setDraft] = useState<string | null>(null);
  let shown = groupIndian(value, scale);
  if (draft !== null) shown = canonicalOf(draft, format) === value ? draft : value;

  const onChange = (event: ChangeEvent<HTMLInputElement>) => {
    const text = replaceInputText(event.currentTarget, (raw) => sanitiseAmountText(raw, format));
    setDraft(text);
    const canonical = canonicalOf(text, format);
    if (canonical !== value) onValueChange(canonical);
  };

  // The grouped text is swapped for the plain one only after the focus event, and before React renders, so a selection
  // made while focusing (select all when tabbing in, select() focusing the input first) carries over to the plain text.
  const handleFocus = (event: FocusEvent<HTMLInputElement>) => {
    const input = event.currentTarget;
    queueMicrotask(() => {
      if (input.ownerDocument.activeElement !== input) return;
      const grouped = input.value;
      if (grouped !== value) {
        const start = input.selectionStart ?? grouped.length;
        const end = input.selectionEnd ?? grouped.length;
        input.value = value;
        if (start === 0 && end === grouped.length) {
          input.select();
        } else {
          input.setSelectionRange(
            ungroupedOffset(grouped, start, value),
            ungroupedOffset(grouped, end, value),
          );
        }
      }
      setDraft(value);
    });
    onFocus?.(event);
  };

  const handleBlur = (event: FocusEvent<HTMLInputElement>) => {
    setDraft(null);
    const canonical = canonicalOf(value, format);
    if (canonical !== value) onValueChange(canonical);
    onBlur?.(event);
  };

  return (
    <InputGroup className={className}>
      <InputGroupAddon>
        <InputGroupText>₹</InputGroupText>
      </InputGroupAddon>
      <InputGroupInput
        {...inputProps}
        type="text"
        inputMode="decimal"
        autoComplete={inputProps.autoComplete ?? "off"}
        value={shown}
        onChange={onChange}
        onFocus={handleFocus}
        onBlur={handleBlur}
      />
    </InputGroup>
  );
}
