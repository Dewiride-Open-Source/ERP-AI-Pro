"use client";

import { Calendar } from "@dewiride/erp-ui/components/ui/calendar";
import { formatDisplayDate, localDateToIsoDate } from "@dewiride/erp-ui/lib/calendar-date";
import { calendarDefaults } from "@dewiride/erp-ui/lib/calendar-defaults";
import { useState } from "react";

import { Specimen, SpecimenRow } from "../specimen";

type DayRange = { from: Date | undefined; to?: Date | undefined };

const specimenToday = new Date(2026, 8, 26);

const sundays = { dayOfWeek: [0] };

function displayed(date: Date | undefined): string {
  return date === undefined ? "none" : formatDisplayDate(localDateToIsoDate(date));
}

export function CalendarSpecimen() {
  const [day, setDay] = useState<Date>(new Date(2026, 8, 15));
  const [range, setRange] = useState<DayRange | undefined>({
    from: new Date(2026, 8, 8),
    to: new Date(2026, 8, 12),
  });

  return (
    <Specimen
      title="Calendar"
      description="The calendar the date inputs open: weeks start on Monday, today is marked and unavailable days are disabled. Today is fixed at 26 September 2026 here so the specimen reads the same every day."
      wide
    >
      <div className="grid min-w-0 gap-6 md:grid-cols-2">
        <SpecimenRow label="One day, with Sundays disabled">
          <div className="grid gap-2">
            <Calendar
              {...calendarDefaults}
              mode="single"
              required
              selected={day}
              onSelect={setDay}
              today={specimenToday}
              defaultMonth={specimenToday}
              disabled={sundays}
              className="rounded-lg border"
            />
            <p className="text-caption text-muted-foreground" data-testid="forms-calendar-day">
              Selected: {displayed(day)}
            </p>
          </div>
        </SpecimenRow>
        <SpecimenRow label="A range of days">
          <div className="grid gap-2">
            <Calendar
              {...calendarDefaults}
              mode="range"
              resetOnSelect
              selected={range}
              onSelect={setRange}
              today={specimenToday}
              defaultMonth={specimenToday}
              className="rounded-lg border"
            />
            <p className="text-caption text-muted-foreground" data-testid="forms-calendar-range">
              Range: {displayed(range?.from)} to {displayed(range?.to)}
            </p>
          </div>
        </SpecimenRow>
      </div>
    </Specimen>
  );
}
