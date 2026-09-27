"use client";

import { CalendarIcon } from "lucide-react";
import { useId, useState, useSyncExternalStore, type FocusEvent, type Ref } from "react";
import type { DateRange } from "react-day-picker";

import { formFieldLabelId } from "@dewiride/erp-ui/components/forms/form-field";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import { Calendar } from "@dewiride/erp-ui/components/ui/calendar";
import { InputGroup } from "@dewiride/erp-ui/components/ui/input-group";
import { Popover, PopoverContent, PopoverTrigger } from "@dewiride/erp-ui/components/ui/popover";
import { isoDateToLocalDate, localDateToIsoDate } from "@dewiride/erp-ui/lib/calendar-date";
import { calendarDefaults } from "@dewiride/erp-ui/lib/calendar-defaults";
import { cn } from "@dewiride/erp-ui/lib/utils";

import { calendarLimits } from "./calendar-limits";
import { DateTextInput } from "./date-text-input";

export type CalendarDateRange = {
  from: string;
  to: string;
};

export type DateRangeInputProps = {
  value: CalendarDateRange;
  onValueChange: (value: CalendarDateRange) => void;
  min?: string | undefined;
  max?: string | undefined;
  id?: string | undefined;
  toId?: string | undefined;
  fromLabel?: string | undefined;
  toLabel?: string | undefined;
  disabled?: boolean | undefined;
  readOnly?: boolean | undefined;
  className?: string | undefined;
  onBlur?: ((event: FocusEvent<HTMLInputElement>) => void) | undefined;
  ref?: Ref<HTMLInputElement> | undefined;
  toRef?: Ref<HTMLInputElement> | undefined;
  "aria-labelledby"?: string | undefined;
  "aria-describedby"?: string | undefined;
  "aria-invalid"?: boolean | "true" | "false" | undefined;
  "aria-required"?: boolean | "true" | "false" | undefined;
};

const twoMonthsQuery = "(min-width: 48rem)";

function subscribeToTwoMonths(onChange: () => void): () => void {
  const query = window.matchMedia(twoMonthsQuery);
  query.addEventListener("change", onChange);
  return () => query.removeEventListener("change", onChange);
}

function rangeOf(value: CalendarDateRange): DateRange | undefined {
  const from = isoDateToLocalDate(value.from);
  return from ? { from, to: isoDateToLocalDate(value.to) } : undefined;
}

export function DateRangeInput({
  value,
  onValueChange,
  min,
  max,
  id,
  toId: toInputId,
  fromLabel = "From",
  toLabel = "To",
  disabled,
  readOnly,
  className,
  onBlur,
  ref,
  toRef,
  "aria-labelledby": labelledBy,
  "aria-describedby": describedBy,
  "aria-invalid": invalid,
  "aria-required": required,
}: DateRangeInputProps) {
  const generatedId = useId();
  const fromId = id ?? `${generatedId}-from`;
  const toId = toInputId ?? `${fromId}-to`;
  const fieldLabelId = labelledBy ?? (id === undefined ? undefined : formFieldLabelId(id));
  const [open, setOpen] = useState(false);
  const [today, setToday] = useState<string | null>(null);
  const [draft, setDraft] = useState<DateRange | undefined>(undefined);
  const twoMonths = useSyncExternalStore(
    subscribeToTwoMonths,
    () => window.matchMedia(twoMonthsQuery).matches,
    () => false,
  );

  const onOpenChange = (next: boolean) => {
    if (next) {
      setToday(localDateToIsoDate(new Date()));
      setDraft(rangeOf(value));
    }
    setOpen(next);
  };

  const onSelect = (range: DateRange | undefined) => {
    if (range?.from && range.to) {
      onValueChange({ from: localDateToIsoDate(range.from), to: localDateToIsoDate(range.to) });
      setOpen(false);
      return;
    }
    setDraft(range);
  };

  const renderPart = (
    partId: string,
    labelId: string,
    label: string,
    partValue: string,
    key: keyof CalendarDateRange,
  ) => (
    <div className="grid min-w-0 gap-1.5">
      <label id={labelId} htmlFor={partId} className="text-caption text-muted-foreground">
        {label}
      </label>
      <InputGroup>
        <DateTextInput
          id={partId}
          ref={key === "from" ? ref : toRef}
          value={partValue}
          onValueChange={(next) => onValueChange({ ...value, [key]: next })}
          onBlur={onBlur}
          disabled={disabled}
          readOnly={readOnly}
          aria-labelledby={fieldLabelId === undefined ? labelId : `${fieldLabelId} ${labelId}`}
          aria-describedby={describedBy}
          aria-invalid={invalid}
          aria-required={required}
        />
      </InputGroup>
    </div>
  );

  return (
    <div className={cn("grid grid-cols-[minmax(0,1fr)_minmax(0,1fr)_auto] items-end gap-2", className)}>
      {renderPart(fromId, `${fromId}-from-label`, fromLabel, value.from, "from")}
      {renderPart(toId, `${toId}-label`, toLabel, value.to, "to")}
      <Popover open={open} onOpenChange={onOpenChange} modal>
        <PopoverTrigger asChild>
          <Button
            type="button"
            variant="outline"
            size="icon"
            aria-label="Choose dates"
            disabled={disabled || readOnly}
          >
            <CalendarIcon aria-hidden="true" />
          </Button>
        </PopoverTrigger>
        <PopoverContent align="end" aria-label="Choose dates" className="w-auto overflow-hidden p-0">
          {today === null ? null : (
            <Calendar
              {...calendarDefaults}
              {...calendarLimits(today, { min, max, shown: value.from === "" ? value.to : value.from })}
              mode="range"
              resetOnSelect
              selected={draft}
              onSelect={onSelect}
              numberOfMonths={twoMonths ? 2 : 1}
              captionLayout="dropdown"
              autoFocus
            />
          )}
        </PopoverContent>
      </Popover>
    </div>
  );
}
