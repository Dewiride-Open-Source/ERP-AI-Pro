"use client";

import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { DateRangeInput, type CalendarDateRange } from "@dewiride/erp-ui/components/pickers/date-range-input";
import { FieldGroup } from "@dewiride/erp-ui/components/ui/field";
import { compareIsoDates, isIsoDate } from "@dewiride/erp-ui/lib/calendar-date";
import { useState } from "react";

import { Specimen } from "../specimen";

function orderProblem({ from, to }: CalendarDateRange): readonly string[] | undefined {
  const reversed = isIsoDate(from) && isIsoDate(to) && compareIsoDates(to, from) < 0;
  return reversed ? ["The end date must be on or after the start date."] : undefined;
}

export function DateRangeSpecimen() {
  const [statement, setStatement] = useState<CalendarDateRange>({ from: "2026-04-01", to: "2026-06-30" });
  const [contract, setContract] = useState<CalendarDateRange>({ from: "2026-06-30", to: "2026-04-01" });

  return (
    <Specimen
      title="Date range"
      description="Two typed dates with one calendar that picks both ends; the values are never reordered, so the form reports an end before the start."
    >
      <FieldGroup>
        <FormField id="ks-forms-statement-period" label="Statement period" required>
          {(frame) => <DateRangeInput {...frame} value={statement} onValueChange={setStatement} />}
        </FormField>
        <p className="text-caption text-muted-foreground" data-testid="forms-date-range-value">
          Value:{" "}
          <span className="font-mono">
            {statement.from === "" ? "(empty)" : statement.from} to{" "}
            {statement.to === "" ? "(empty)" : statement.to}
          </span>
        </p>
        <FormField id="ks-forms-contract-period" label="Contract period" errors={orderProblem(contract)}>
          {(frame) => <DateRangeInput {...frame} value={contract} onValueChange={setContract} />}
        </FormField>
        <FormField
          id="ks-forms-locked-period"
          label="Locked period"
          description="Closed by the accountant."
          disabled
        >
          {(frame) => (
            <DateRangeInput
              {...frame}
              value={{ from: "2025-04-01", to: "2026-03-31" }}
              onValueChange={() => undefined}
              disabled
            />
          )}
        </FormField>
      </FieldGroup>
    </Specimen>
  );
}
