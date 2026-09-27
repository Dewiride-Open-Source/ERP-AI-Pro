"use client";

import {
  useRef,
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
  const pressedToFocus = useRef(false);
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

  // A pressed pointer places the caret in the grouped text only after the focus event, so the swap waits for the
  // release; any other focus swaps before React renders, so a selection made while focusing carries over.
  const handleMouseDown = (event: MouseEvent<HTMLInputElement>) => {
    onMouseDown?.(event);
    const input = event.currentTarget;
    if (event.defaultPrevented || input.ownerDocument.activeElement === input) return;
    pressedToFocus.current = true;
    input.ownerDocument.addEventListener(
      "mouseup",
      () => {
        pressedToFocus.current = false;
        showPlainText(input);
      },
      { capture: true, once: true },
    );
  };

  const handleFocus = (event: FocusEvent<HTMLInputElement>) => {
    const input = event.currentTarget;
    if (!pressedToFocus.current) queueMicrotask(() => showPlainText(input));
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
        onMouseDown={handleMouseDown}
        onFocus={handleFocus}
        onBlur={handleBlur}
      />
    </InputGroup>
  );
}
