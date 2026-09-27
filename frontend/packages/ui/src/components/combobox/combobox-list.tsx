"use client";

import { CheckIcon } from "lucide-react";
import { useCallback } from "react";

import { Spinner } from "@dewiride/erp-ui/components/ui/spinner";

import type { ComboboxOption } from "./combobox-options";

export type ComboboxListProps = {
  id: string;
  options: readonly ComboboxOption[];
  value: string | null;
  activeIndex: number;
  listed: boolean;
  loading: boolean;
  loadingMessage: string;
  emptyMessage: string;
  optionId: (index: number) => string;
  onSelect: (option: ComboboxOption) => void;
  onActivate: (option: ComboboxOption) => void;
  "aria-labelledby"?: string | undefined;
  "aria-label"?: string | undefined;
};

function keepInView(list: HTMLElement, option: HTMLElement): void {
  const top = option.offsetTop;
  const bottom = top + option.offsetHeight;
  if (top < list.scrollTop) list.scrollTop = top;
  else if (bottom > list.scrollTop + list.clientHeight) list.scrollTop = bottom - list.clientHeight;
}

export function ComboboxList({
  id,
  options,
  value,
  activeIndex,
  listed,
  loading,
  loadingMessage,
  emptyMessage,
  optionId,
  onSelect,
  onActivate,
  "aria-labelledby": labelledBy,
  "aria-label": label,
}: ComboboxListProps) {
  // Only the list scrolls: the popup is still being positioned when an option first becomes active, and scrolling the
  // option into view through the page would move the page instead.
  const listRef = useCallback(
    (list: HTMLUListElement | null) => {
      const option = activeIndex < 0 ? null : list?.children.item(activeIndex);
      if (list && option instanceof HTMLElement) keepInView(list, option);
    },
    [activeIndex],
  );

  if (!listed) {
    return (
      <p aria-hidden="true" className="flex items-center gap-2 px-2 py-1.5 text-sm text-muted-foreground">
        {loading ? <Spinner aria-hidden="true" /> : null}
        {loading ? loadingMessage : emptyMessage}
      </p>
    );
  }

  return (
    <ul
      ref={listRef}
      id={id}
      role="listbox"
      aria-labelledby={labelledBy}
      aria-label={labelledBy === undefined ? label : undefined}
      className="relative max-h-72 overflow-y-auto"
    >
      {options.map((option, index) => {
        const active = index === activeIndex;
        const chosen = option.value === value;
        return (
          <li
            key={option.value}
            id={optionId(index)}
            role="option"
            aria-selected={active}
            aria-disabled={option.disabled || undefined}
            data-active={active || undefined}
            data-checked={chosen || undefined}
            onClick={() => onSelect(option)}
            onMouseMove={() => {
              if (!option.disabled && !active) onActivate(option);
            }}
            className="flex cursor-default items-center gap-2 rounded-md px-2 py-1.5 text-sm select-none aria-disabled:opacity-50 data-active:bg-accent data-active:text-accent-foreground data-active:**:text-accent-foreground"
          >
            <span className="grid min-w-0 flex-1 gap-0.5">
              <span className="truncate">{option.label}</span>
              {option.description ? (
                <span className="text-caption text-muted-foreground">{option.description}</span>
              ) : null}
            </span>
            {chosen ? <CheckIcon aria-hidden="true" className="size-4 shrink-0" /> : null}
          </li>
        );
      })}
    </ul>
  );
}
