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
  const shown = list.getBoundingClientRect();
  const wanted = option.getBoundingClientRect();
  const scale = shown.height / Number.parseFloat(getComputedStyle(list).height) || 1;
  if (wanted.top < shown.top) list.scrollTop -= Math.ceil((shown.top - wanted.top) / scale);
  else if (wanted.bottom > shown.bottom) list.scrollTop += Math.ceil((wanted.bottom - shown.bottom) / scale);
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
  // option into view through the page would move the page instead. Radix caps the popup at the space it measured only
  // after that first placement, so the option is kept in view again whenever the list's own height changes. The list
  // fits inside that space less the popup's padding, rounded down to whole pixels: Chromium rounds a scroll range to
  // whole pixels, so a list of fractional height could never show its last option in full. Options follow the reader's
  // font size and can still sit at fractional offsets, so each is measured on screen and divided by the scale of the
  // popup's opening animation to get back to the list's own pixels.
  const listRef = useCallback(
    (list: HTMLUListElement | null) => {
      const option = activeIndex < 0 ? null : list?.children.item(activeIndex);
      if (!list || !(option instanceof HTMLElement)) return undefined;
      keepInView(list, option);
      const resizes = new ResizeObserver(() => keepInView(list, option));
      resizes.observe(list);
      return () => resizes.disconnect();
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
      className="relative max-h-[round(down,min(--spacing(72),var(--radix-popover-content-available-height)_-_--spacing(2)),1px)] min-h-0 overflow-y-auto"
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
