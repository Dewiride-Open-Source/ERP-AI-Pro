"use client";

import {
  useEffect,
  useState,
  type ChangeEvent,
  type ComponentProps,
  type FocusEvent,
  type MouseEvent,
} from "react";

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

import { createPressWatch } from "./press-watch";
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
  onMouseDown,
  ...inputProps
}: AmountInputProps) {
  const format: AmountFormat = { scale, allowNegative };
  const [draft, setDraft] = useState<string | null>(null);
  const [press] = useState(createPressWatch);
  useEffect(() => () => press.cancel(), [press]);
  const grouped = groupIndian(value, scale);
  let shown = grouped;
  if (draft !== null) shown = canonicalOf(draft, format) === value ? draft : value;

  const onChange = (event: ChangeEvent<HTMLInputElement>) => {
    const text = replaceInputText(event.currentTarget, (raw) => sanitiseAmountText(raw, format));
    setDraft(text);
    const canonical = canonicalOf(text, format);
    if (canonical !== value) onValueChange(canonical);
  };

  const showPlainText = (input: HTMLInputElement) => {
    if (input.ownerDocument.activeElement !== input || input.value !== grouped) return;
    if (grouped !== value) {
      const start = input.selectionStart ?? grouped.length;
      const end = input.selectionEnd ?? grouped.length;
      input.value = value;
      if (start === 0 && end === grouped.length) {
        input.select();
      } else {
        input.setSelectionRange(ungroupedOffset(grouped, start, value), ungroupedOffset(grouped, end, value));
      }
    }
    setDraft(value);
  };

  // A primary-button press, the one that places the caret for typing, sets it in the grouped text only after the focus
  // event, so the swap waits for the press to end; any other focus swaps before React renders, so a selection made
  // while focusing carries over. Losing focus ends the wait, so a press whose end never reached the page cannot hold
  // back the swap of a later focus.
  const handleMouseDown = (event: MouseEvent<HTMLInputElement>) => {
    onMouseDown?.(event);
    const input = event.currentTarget;
    if (event.defaultPrevented || event.button !== 0 || input.ownerDocument.activeElement === input) return;
    press.start(input.ownerDocument, () => showPlainText(input));
  };

  const handleFocus = (event: FocusEvent<HTMLInputElement>) => {
    const input = event.currentTarget;
    if (!press.pending) queueMicrotask(() => showPlainText(input));
    onFocus?.(event);
  };

  const handleBlur = (event: FocusEvent<HTMLInputElement>) => {
    press.cancel();
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
        onMouseDown={handleMouseDown}
        onFocus={handleFocus}
        onBlur={handleBlur}
      />
    </InputGroup>
  );
}
