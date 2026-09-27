"use client";

import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { DateInput } from "@dewiride/erp-ui/components/pickers/date-input";
import { FieldGroup } from "@dewiride/erp-ui/components/ui/field";
import { isIsoDate } from "@dewiride/erp-ui/lib/calendar-date";
import { useState } from "react";

import { Specimen } from "../specimen";

const realDateMessage = "Enter a real date as day-month-year, for example 31-03-2026.";

export function DateSpecimen() {
  const [invoiceDate, setInvoiceDate] = useState("2026-03-31");
  const [dueDate, setDueDate] = useState("");
  const [deliveryDate, setDeliveryDate] = useState("31-02-2026");

  return (
    <Specimen
      title="Date"
      description="Typed as day-month-year with dashes, slashes or dots, or chosen from the calendar; the value is yyyy-MM-dd."
    >
      <FieldGroup>
        <FormField
          id="ks-forms-invoice-date"
          label="Invoice date"
          description="Within the financial year 2025–26; the calendar disables every other day."
        >
          {(frame) => (
            <DateInput
              {...frame}
              value={invoiceDate}
              onValueChange={setInvoiceDate}
              min="2025-04-01"
              max="2026-03-31"
            />
          )}
        </FormField>
        <p className="text-caption text-muted-foreground" data-testid="forms-date-value">
          Value: <span className="font-mono">{invoiceDate === "" ? "(empty)" : invoiceDate}</span>
        </p>
        <FormField id="ks-forms-due-date" label="Due date" required>
          {(frame) => <DateInput {...frame} value={dueDate} onValueChange={setDueDate} />}
        </FormField>
        <FormField
          id="ks-forms-delivery-date"
          label="Delivery date"
          errors={deliveryDate === "" || isIsoDate(deliveryDate) ? undefined : [realDateMessage]}
        >
          {(frame) => <DateInput {...frame} value={deliveryDate} onValueChange={setDeliveryDate} />}
        </FormField>
        <FormField
          id="ks-forms-posting-date"
          label="Posting date"
          description="Set when the voucher is posted."
          disabled
        >
          {(frame) => <DateInput {...frame} value="2026-04-01" onValueChange={() => undefined} disabled />}
        </FormField>
      </FieldGroup>
    </Specimen>
  );
}
