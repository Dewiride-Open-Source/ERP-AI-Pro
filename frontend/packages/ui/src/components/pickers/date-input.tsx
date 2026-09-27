"use client";

import { CalendarIcon } from "lucide-react";
import { useState } from "react";

import { Calendar } from "@dewiride/erp-ui/components/ui/calendar";
import { InputGroup, InputGroupAddon, InputGroupButton } from "@dewiride/erp-ui/components/ui/input-group";
import { Popover, PopoverContent, PopoverTrigger } from "@dewiride/erp-ui/components/ui/popover";
import { isoDateToLocalDate, localDateToIsoDate } from "@dewiride/erp-ui/lib/calendar-date";
import { calendarDefaults } from "@dewiride/erp-ui/lib/calendar-defaults";

import { calendarLimits } from "./calendar-limits";
import { DateTextInput, type DateTextInputProps } from "./date-text-input";

export type DateInputProps = DateTextInputProps & {
  min?: string | undefined;
  max?: string | undefined;
};

export function DateInput({
  value,
  onValueChange,
  min,
  max,
  className,
  disabled,
  readOnly,
  ...inputProps
}: DateInputProps) {
  const [open, setOpen] = useState(false);
  const [today, setToday] = useState<string | null>(null);
  const selected = isoDateToLocalDate(value);

  const onOpenChange = (next: boolean) => {
    if (next) setToday(localDateToIsoDate(new Date()));
    setOpen(next);
  };

  const onSelect = (date: Date) => {
    const picked = localDateToIsoDate(date);
    if (picked !== value) onValueChange(picked);
    setOpen(false);
  };

  return (
    <InputGroup className={className}>
      <DateTextInput
        {...inputProps}
        value={value}
        onValueChange={onValueChange}
        disabled={disabled}
        readOnly={readOnly}
      />
      <InputGroupAddon align="inline-end">
        <Popover open={open} onOpenChange={onOpenChange} modal>
          <PopoverTrigger asChild>
            <InputGroupButton size="icon-xs" aria-label="Choose date" disabled={disabled || readOnly}>
              <CalendarIcon aria-hidden="true" />
            </InputGroupButton>
          </PopoverTrigger>
          <PopoverContent align="end" aria-label="Choose date" className="w-auto overflow-hidden p-0">
            {today === null ? null : (
              <Calendar
                {...calendarDefaults}
                {...calendarLimits(today, { min, max, shown: value })}
                mode="single"
                required
                selected={selected}
                onSelect={onSelect}
                captionLayout="dropdown"
                autoFocus
              />
            )}
          </PopoverContent>
        </Popover>
      </InputGroupAddon>
    </InputGroup>
  );
}
