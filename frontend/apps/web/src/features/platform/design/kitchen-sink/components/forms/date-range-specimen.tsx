"use client";

import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { DateRangeInput, type CalendarDateRange } from "@dewiride/erp-ui/components/pickers/date-range-input";
import { FieldGroup } from "@dewiride/erp-ui/components/ui/field";
import { useState } from "react";

import { dateRangeSchema } from "@/shared/forms/schemas/field-schemas";

import { rangeEndState } from "../../../form-kit/components/range-end-state";
import { Specimen } from "../specimen";

import { schemaProblems } from "./schema-problems";

const contractPeriodSchema = dateRangeSchema({ required: false });

export function DateRangeSpecimen() {
  const [statement, setStatement] = useState<CalendarDateRange>({ from: "2026-04-01", to: "2026-06-30" });
  const [contract, setContract] = useState<CalendarDateRange>({ from: "2026-06-30", to: "2026-04-01" });
  const contractFromProblems = schemaProblems(contractPeriodSchema, contract, ["from"]);
  const contractToProblems = schemaProblems(contractPeriodSchema, contract, ["to"]);

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
        <FormField
          id="ks-forms-contract-period"
          label="Contract period"
          errors={[...(contractFromProblems ?? []), ...(contractToProblems ?? [])]}
        >
          {(frame) => (
            <DateRangeInput
              {...frame}
              ends={{
                from: rangeEndState(frame, contractFromProblems),
                to: rangeEndState(frame, contractToProblems),
              }}
              value={contract}
              onValueChange={setContract}
            />
          )}
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
