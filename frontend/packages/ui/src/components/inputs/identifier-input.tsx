"use client";

import { useState, type ChangeEvent, type ComponentProps, type CompositionEvent } from "react";

import { Input } from "@dewiride/erp-ui/components/ui/input";
import { normaliseIdentifier } from "@dewiride/erp-ui/lib/identifiers";
import { cn } from "@dewiride/erp-ui/lib/utils";

import { replaceInputText } from "./replace-input-text";

export type IdentifierInputProps = Omit<
  ComponentProps<"input">,
  "value" | "defaultValue" | "onChange" | "type" | "maxLength"
> & {
  value: string;
  onValueChange: (value: string) => void;
  maxLength: number;
};

export function IdentifierInput({
  value,
  onValueChange,
  maxLength,
  className,
  onCompositionEnd,
  ...inputProps
}: IdentifierInputProps) {
  const [composition, setComposition] = useState<string | null>(null);

  const commit = (input: HTMLInputElement) => {
    const text = replaceInputText(input, (raw) => normaliseIdentifier(raw, maxLength));
    if (text !== value) onValueChange(text);
  };

  // Rewriting the text while an input method is still composing makes mobile keyboards repeat characters, so the
  // composed text is shown as typed and normalised once the composition ends.
  const onChange = (event: ChangeEvent<HTMLInputElement>) => {
    if (event.nativeEvent instanceof InputEvent && event.nativeEvent.isComposing) {
      setComposition(event.currentTarget.value);
      return;
    }
    setComposition(null);
    commit(event.currentTarget);
  };

  const handleCompositionEnd = (event: CompositionEvent<HTMLInputElement>) => {
    setComposition(null);
    commit(event.currentTarget);
    onCompositionEnd?.(event);
  };

  return (
    <Input
      {...inputProps}
      type="text"
      autoComplete="off"
      autoCapitalize="characters"
      autoCorrect="off"
      spellCheck={false}
      className={cn("font-mono", className)}
      value={composition ?? value}
      onChange={onChange}
      onCompositionEnd={handleCompositionEnd}
    />
  );
}
