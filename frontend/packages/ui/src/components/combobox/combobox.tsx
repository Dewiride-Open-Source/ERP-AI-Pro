"use client";

import { ChevronDownIcon, XIcon } from "lucide-react";
import {
  useId,
  useImperativeHandle,
  useRef,
  useState,
  type ChangeEvent,
  type ComponentProps,
  type FocusEvent,
  type KeyboardEvent,
  type MouseEvent,
} from "react";

import {
  InputGroup,
  InputGroupAddon,
  InputGroupButton,
  InputGroupInput,
} from "@dewiride/erp-ui/components/ui/input-group";
import { Popover, PopoverAnchor, PopoverContent } from "@dewiride/erp-ui/components/ui/popover";

import { ComboboxList } from "./combobox-list";
import {
  filterComboboxOptions,
  firstEnabledIndex,
  lastEnabledIndex,
  nextEnabledIndex,
  type ComboboxDirection,
  type ComboboxOption,
} from "./combobox-options";

export type { ComboboxOption } from "./combobox-options";

export type ComboboxProps = Omit<
  ComponentProps<"input">,
  | "value"
  | "defaultValue"
  | "onChange"
  | "type"
  | "role"
  | "children"
  | "autoComplete"
  | "aria-autocomplete"
  | "aria-expanded"
  | "aria-controls"
  | "aria-activedescendant"
> & {
  options: readonly ComboboxOption[];
  value: string | null;
  onValueChange: (value: string | null) => void;
  emptyMessage: string;
  inputValue?: string | undefined;
  onInputValueChange?: ((text: string) => void) | undefined;
  loading?: boolean | undefined;
  loadingMessage?: string | undefined;
  clearLabel?: string | undefined;
  toggleLabel?: string | undefined;
};

type Activation = "first" | "last" | "none";

function keepFocusOnTheInput(event: MouseEvent): void {
  event.preventDefault();
}

function findSelected(
  options: readonly ComboboxOption[],
  value: string | null,
  remembered: ComboboxOption | null,
): ComboboxOption | undefined {
  if (value === null) return undefined;
  const listed = options.find((option) => option.value === value);
  if (listed) return listed;
  return remembered?.value === value ? remembered : undefined;
}

function labelIdOf(input: HTMLInputElement | null): string | undefined {
  return input?.labels?.[0]?.id || undefined;
}

export function Combobox({
  options,
  value,
  onValueChange,
  emptyMessage,
  inputValue,
  onInputValueChange,
  loading = false,
  loadingMessage = "Searching…",
  clearLabel = "Clear",
  toggleLabel = "Show options",
  className,
  disabled = false,
  readOnly = false,
  ref,
  onBlur,
  onClick,
  onKeyDown,
  "aria-label": ariaLabel,
  "aria-labelledby": ariaLabelledBy,
  ...inputProps
}: ComboboxProps) {
  const baseId = useId();
  const listboxId = `${baseId}-listbox`;
  const optionId = (index: number) => `${baseId}-option-${index}`;
  const inputRef = useRef<HTMLInputElement>(null);
  const anchorRef = useRef<HTMLDivElement>(null);
  useImperativeHandle(ref, () => inputRef.current as HTMLInputElement, []);

  const [open, setOpen] = useState(false);
  const [typed, setTyped] = useState<string | null>(null);
  const [activeValue, setActiveValue] = useState<string | null>(null);
  const [remembered, setRemembered] = useState<ComboboxOption | null>(null);
  const [labelLookup, setLabelLookup] = useState<string | undefined>(undefined);

  const selectedLabel = findSelected(options, value, remembered)?.label ?? "";
  const text = inputValue ?? typed ?? selectedLabel;
  const visibleOptions =
    onInputValueChange === undefined ? filterComboboxOptions(options, typed ?? "") : options;
  const activeIndex =
    activeValue === null ? -1 : visibleOptions.findIndex((option) => option.value === activeValue);
  const locked = disabled || readOnly;
  const popupOpen = open && !locked;
  const listed = popupOpen && !loading && visibleOptions.length > 0;

  let status = "";
  if (popupOpen && loading) status = loadingMessage;
  else if (popupOpen && visibleOptions.length === 0) status = emptyMessage;

  const reportText = (next: string) => {
    if (onInputValueChange && next !== text) onInputValueChange(next);
  };

  const showPopup = () => {
    setLabelLookup(labelIdOf(inputRef.current));
    setOpen(true);
  };

  const openPopup = (activation: Activation) => {
    showPopup();
    if (activation === "none") {
      setActiveValue(null);
      return;
    }
    let index = visibleOptions.findIndex((option) => option.value === value && !option.disabled);
    if (index < 0) {
      index = activation === "first" ? firstEnabledIndex(visibleOptions) : lastEnabledIndex(visibleOptions);
    }
    setActiveValue(visibleOptions[index]?.value ?? null);
  };

  const closePopup = () => {
    setOpen(false);
    setActiveValue(null);
  };

  const revertText = () => {
    setTyped(null);
    reportText(selectedLabel);
  };

  const move = (direction: ComboboxDirection) => {
    const index = nextEnabledIndex(visibleOptions, activeIndex, direction);
    setActiveValue(visibleOptions[index]?.value ?? null);
  };

  const select = (option: ComboboxOption) => {
    if (option.disabled) return;
    setRemembered(option);
    setTyped(null);
    closePopup();
    reportText(option.label);
    if (option.value !== value) onValueChange(option.value);
  };

  const clear = () => {
    setTyped(null);
    closePopup();
    reportText("");
    if (value !== null) onValueChange(null);
    inputRef.current?.focus();
  };

  const toggle = () => {
    if (popupOpen) closePopup();
    else openPopup("none");
    inputRef.current?.focus();
  };

  const handleChange = (event: ChangeEvent<HTMLInputElement>) => {
    const next = event.currentTarget.value;
    setTyped(next);
    setActiveValue(null);
    reportText(next);
    if (next === "" && value !== null) onValueChange(null);
    if (!open) showPopup();
  };

  const handleVerticalArrow = (event: KeyboardEvent<HTMLInputElement>, direction: ComboboxDirection) => {
    event.preventDefault();
    if (event.altKey) {
      if (direction === 1 && !popupOpen) openPopup("none");
      if (direction === -1) closePopup();
    } else if (!popupOpen) {
      openPopup(direction === 1 ? "first" : "last");
    } else if (listed) {
      move(direction);
    }
  };

  const handleEnter = (event: KeyboardEvent<HTMLInputElement>) => {
    if (!popupOpen) return;
    event.preventDefault();
    const option = listed ? visibleOptions[activeIndex] : undefined;
    if (option) select(option);
    else closePopup();
  };

  const handleEscape = (event: KeyboardEvent<HTMLInputElement>) => {
    if (popupOpen) {
      event.preventDefault();
      closePopup();
    } else if (text !== selectedLabel) {
      event.preventDefault();
      revertText();
    }
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    onKeyDown?.(event);
    if (locked || event.nativeEvent.isComposing) return;
    if (event.key === "ArrowDown") handleVerticalArrow(event, 1);
    else if (event.key === "ArrowUp") handleVerticalArrow(event, -1);
    else if (event.key === "Enter") handleEnter(event);
    else if (event.key === "Escape") handleEscape(event);
    else if ((event.key === "Home" || event.key === "End") && activeValue !== null) setActiveValue(null);
  };

  const handleClick = (event: MouseEvent<HTMLInputElement>) => {
    onClick?.(event);
    if (!locked && !popupOpen) openPopup("none");
  };

  const handleBlur = (event: FocusEvent<HTMLInputElement>) => {
    closePopup();
    if (text !== selectedLabel) revertText();
    onBlur?.(event);
  };

  return (
    <Popover
      open={popupOpen}
      onOpenChange={(next) => {
        if (!next) closePopup();
      }}
    >
      <PopoverAnchor asChild>
        <InputGroup ref={anchorRef} className={className}>
          <InputGroupInput
            {...inputProps}
            ref={inputRef}
            type="text"
            role="combobox"
            aria-label={ariaLabel}
            aria-labelledby={ariaLabelledBy}
            aria-autocomplete="list"
            aria-expanded={listed}
            aria-controls={listed ? listboxId : undefined}
            aria-activedescendant={listed && activeIndex >= 0 ? optionId(activeIndex) : undefined}
            autoComplete="off"
            spellCheck={false}
            disabled={disabled}
            readOnly={readOnly}
            value={text}
            onChange={handleChange}
            onKeyDown={handleKeyDown}
            onClick={handleClick}
            onBlur={handleBlur}
          />
          <InputGroupAddon align="inline-end">
            {!locked && (value !== null || text !== "") ? (
              <InputGroupButton
                size="icon-xs"
                tabIndex={-1}
                aria-label={clearLabel}
                onMouseDown={keepFocusOnTheInput}
                onClick={clear}
              >
                <XIcon aria-hidden="true" />
              </InputGroupButton>
            ) : null}
            <InputGroupButton
              size="icon-xs"
              tabIndex={-1}
              aria-label={toggleLabel}
              aria-expanded={listed}
              aria-controls={listed ? listboxId : undefined}
              disabled={locked}
              onMouseDown={keepFocusOnTheInput}
              onClick={toggle}
            >
              <ChevronDownIcon aria-hidden="true" />
            </InputGroupButton>
          </InputGroupAddon>
          <span role="status" className="sr-only">
            {status}
          </span>
        </InputGroup>
      </PopoverAnchor>
      <PopoverContent
        role="presentation"
        align="start"
        className="w-(--radix-popover-trigger-width) gap-0 p-1"
        onOpenAutoFocus={(event) => event.preventDefault()}
        onCloseAutoFocus={(event) => event.preventDefault()}
        onEscapeKeyDown={(event) => event.preventDefault()}
        onInteractOutside={(event) => {
          if (event.target instanceof Node && anchorRef.current?.contains(event.target))
            event.preventDefault();
        }}
        onMouseDown={keepFocusOnTheInput}
      >
        <ComboboxList
          id={listboxId}
          options={visibleOptions}
          value={value}
          activeIndex={activeIndex}
          listed={listed}
          loading={loading}
          loadingMessage={loadingMessage}
          emptyMessage={emptyMessage}
          optionId={optionId}
          onSelect={select}
          onActivate={(option) => setActiveValue(option.value)}
          aria-labelledby={ariaLabelledBy ?? labelLookup}
          aria-label={ariaLabel}
        />
      </PopoverContent>
    </Popover>
  );
}
