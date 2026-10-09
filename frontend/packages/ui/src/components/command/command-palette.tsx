"use client";

import { SearchIcon } from "lucide-react";
import {
  useId,
  useRef,
  useState,
  type KeyboardEvent,
  type MouseEvent,
  type ReactNode,
  type RefObject,
} from "react";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import { Dialog, DialogClose, DialogContent, DialogTitle } from "@dewiride/erp-ui/components/ui/dialog";
import { Kbd } from "@dewiride/erp-ui/components/ui/kbd";

import {
  commandSections,
  filterCommands,
  nextCommandIndex,
  type CommandItem,
} from "./command-palette-model.ts";

export type { CommandItem } from "./command-palette-model.ts";

export interface CommandPaletteItem extends CommandItem {
  readonly icon?: ReactNode;
}

export interface CommandPaletteProps<T extends CommandPaletteItem> {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  items: readonly T[];
  onSelect: (item: T) => void;
  title: string;
  inputLabel: string;
  placeholder: string;
  listLabel: string;
  emptyMessage: string;
  countMessage: (count: number) => string;
  returnFocusTo?: RefObject<HTMLElement | null> | undefined;
}

type SearchProps<T extends CommandPaletteItem> = Omit<
  CommandPaletteProps<T>,
  "open" | "onOpenChange" | "title" | "returnFocusTo"
> & {
  hintId: string;
};

// The palette opens from state, not from a dialog trigger, so Radix would return focus to nothing when it closes. Radix
// runs the open handler before it moves focus inside, so the element the person was using is still the active one. A
// dismissed palette sends focus back there, or to returnFocusTo when that was the page itself, a container around
// returnFocusTo (WebKit focuses the nearest focusable ancestor of a button it clicks) or is gone; after a choice,
// which usually changes the page under the palette and so removes that element moments later, focus goes to returnFocusTo.
// The content unmounts only after its exit animation, so focus that another layer or the chosen page has taken by then
// stays where it is.
export function CommandPalette<T extends CommandPaletteItem>({
  open,
  onOpenChange,
  onSelect,
  title,
  returnFocusTo,
  ...search
}: CommandPaletteProps<T>) {
  const hintId = useId();
  const focusBeforeOpen = useRef<Element | null>(null);
  const chose = useRef(false);

  const rememberFocus = () => {
    focusBeforeOpen.current = document.activeElement;
    chose.current = false;
  };

  const select = (item: T) => {
    chose.current = true;
    onSelect(item);
  };

  const restoreFocus = (event: Event) => {
    event.preventDefault();
    const previous = focusBeforeOpen.current;
    focusBeforeOpen.current = null;
    if (document.activeElement !== null && document.activeElement !== document.body) return;
    const opener = returnFocusTo?.current;
    const usable =
      (previous instanceof HTMLElement || previous instanceof SVGElement) &&
      previous !== document.body &&
      previous.isConnected &&
      !(opener && previous.contains(opener));
    const target = chose.current && opener ? opener : usable ? previous : opener;
    target?.focus({ preventScroll: true });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        showCloseButton={false}
        aria-describedby={hintId}
        onOpenAutoFocus={rememberFocus}
        onCloseAutoFocus={restoreFocus}
        onMouseDown={keepFocusInTheSearch}
        className="top-24 flex max-h-[calc(100dvh-8rem)] translate-y-0 flex-col gap-0 overflow-hidden p-0 sm:max-w-lg"
      >
        <DialogTitle className="sr-only">{title}</DialogTitle>
        <CommandSearch {...search} onSelect={select} hintId={hintId} />
      </DialogContent>
    </Dialog>
  );
}

function keepFocusInTheSearch(event: MouseEvent<HTMLDivElement>): void {
  if (event.target instanceof Element && !event.target.closest("input, button")) event.preventDefault();
}

// Only the list scrolls, measured in its own offsets, because the dialog is still scaling in when it opens. The ref sits
// on the active option alone, so it runs again whenever another option becomes active.
function keepActiveOptionInView(option: HTMLDivElement | null): void {
  const list = option?.closest('[role="listbox"]');
  if (!option || !(list instanceof HTMLElement)) return;
  const top = option.offsetTop;
  const bottom = top + option.offsetHeight;
  if (top < list.scrollTop) list.scrollTop = top;
  else if (bottom > list.scrollTop + list.clientHeight) list.scrollTop = bottom - list.clientHeight;
}

// The dialog unmounts its content when it closes, so every opening starts with an empty search and the first item active.
function CommandSearch<T extends CommandPaletteItem>({
  items,
  onSelect,
  inputLabel,
  placeholder,
  listLabel,
  emptyMessage,
  countMessage,
  hintId,
}: SearchProps<T>) {
  const baseId = useId();
  const listboxId = `${baseId}-listbox`;
  const optionId = (index: number) => `${baseId}-option-${index}`;
  const [query, setQuery] = useState("");
  const [activeIndex, setActiveIndex] = useState(0);

  const results = filterCommands(items, query);
  const active = results.length === 0 ? -1 : Math.min(Math.max(activeIndex, 0), results.length - 1);
  const listed = results.length > 0;
  let status = "";
  if (query.trim() !== "") status = listed ? countMessage(results.length) : emptyMessage;

  const choose = (item: T | undefined) => {
    if (item !== undefined) onSelect(item);
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.nativeEvent.isComposing) return;
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      setActiveIndex(nextCommandIndex(results.length, active, event.key === "ArrowDown" ? 1 : -1));
    } else if (event.key === "Enter") {
      event.preventDefault();
      choose(results[active]);
    }
  };

  return (
    <>
      <div className="flex items-center gap-2 border-b px-3 py-2">
        <SearchIcon aria-hidden="true" className="size-4 shrink-0 text-muted-foreground" />
        <input
          type="text"
          role="combobox"
          aria-label={inputLabel}
          aria-autocomplete="list"
          aria-expanded={listed}
          aria-controls={listed ? listboxId : undefined}
          aria-activedescendant={active >= 0 ? optionId(active) : undefined}
          autoComplete="off"
          spellCheck={false}
          placeholder={placeholder}
          value={query}
          onChange={(event) => {
            setQuery(event.currentTarget.value);
            setActiveIndex(0);
          }}
          onKeyDown={handleKeyDown}
          className="h-9 min-w-0 flex-1 rounded-md bg-transparent px-1 text-sm focus-ring placeholder:text-muted-foreground"
        />
        <DialogClose asChild>
          <Button variant="ghost" size="xs" tabIndex={-1} className="px-1">
            <span className="sr-only">Close</span>
            <Kbd>Esc</Kbd>
          </Button>
        </DialogClose>
      </div>
      <span role="status" className="sr-only">
        {status}
      </span>
      {listed ? (
        <div
          id={listboxId}
          role="listbox"
          aria-label={listLabel}
          className="relative max-h-80 min-h-0 flex-1 overflow-y-auto p-2"
        >
          {commandSections(results).map((section, sectionIndex) => (
            <CommandGroup
              key={section.entries[0]?.item.id ?? sectionIndex}
              group={section.group}
              headingId={`${baseId}-group-${sectionIndex}`}
            >
              {section.entries.map(({ item, index }) => (
                <div
                  key={item.id}
                  ref={index === active ? keepActiveOptionInView : undefined}
                  id={optionId(index)}
                  role="option"
                  aria-selected={index === active}
                  aria-labelledby={`${optionId(index)}-label`}
                  aria-describedby={item.description ? `${optionId(index)}-description` : undefined}
                  data-active={index === active || undefined}
                  onClick={() => choose(item)}
                  onMouseMove={() => {
                    if (index !== active) setActiveIndex(index);
                  }}
                  className="flex cursor-default items-center gap-3 rounded-md px-2 py-2 text-sm select-none data-active:bg-accent data-active:text-accent-foreground data-active:outline-2 data-active:-outline-offset-2 data-active:outline-ring data-active:**:text-accent-foreground [&_svg]:size-4 [&_svg]:shrink-0 [&_svg]:text-muted-foreground"
                >
                  {item.icon}
                  <span className="grid min-w-0 flex-1 gap-0.5">
                    <span id={`${optionId(index)}-label`} className="truncate font-medium">
                      {item.label}
                    </span>
                    {item.description ? (
                      <span
                        id={`${optionId(index)}-description`}
                        className="truncate text-caption text-muted-foreground"
                      >
                        {item.description}
                      </span>
                    ) : null}
                  </span>
                </div>
              ))}
            </CommandGroup>
          ))}
        </div>
      ) : (
        <p className="px-4 py-8 text-center text-sm text-muted-foreground">{emptyMessage}</p>
      )}
      <p
        id={hintId}
        className="flex items-center gap-1.5 border-t px-3 py-2 text-caption text-muted-foreground max-sm:hidden"
      >
        <Kbd>↑</Kbd>
        <Kbd>↓</Kbd>
        <span>to move,</span>
        <Kbd>Enter</Kbd>
        <span>to open,</span>
        <Kbd>Esc</Kbd>
        <span>to close.</span>
      </p>
    </>
  );
}

function CommandGroup({
  group,
  headingId,
  children,
}: {
  group: string | undefined;
  headingId: string;
  children: ReactNode;
}) {
  if (group === undefined) return children;
  return (
    <div role="group" aria-labelledby={headingId} className="mt-2 first:mt-0">
      <div id={headingId} className="px-2 pt-1 pb-1.5 text-eyebrow text-muted-foreground uppercase">
        {group}
      </div>
      {children}
    </div>
  );
}
