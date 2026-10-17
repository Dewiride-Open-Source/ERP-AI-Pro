"use client";

import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { DateInput } from "@dewiride/erp-ui/components/pickers/date-input";
import { DateRangeInput, type CalendarDateRange } from "@dewiride/erp-ui/components/pickers/date-range-input";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@dewiride/erp-ui/components/ui/dialog";
import { FieldGroup } from "@dewiride/erp-ui/components/ui/field";
import { useState } from "react";

import { calendarDateSchema } from "@/shared/forms/schemas/field-schemas";

import { Specimen } from "../specimen";

import { schemaProblems } from "./schema-problems";

const deliveryDateSchema = calendarDateSchema({ required: false });

export function DateSpecimen() {
  const [invoiceDate, setInvoiceDate] = useState("2026-03-31");
  const [dueDate, setDueDate] = useState("");
  const [deliveryDate, setDeliveryDate] = useState("31-02-2026");
  const [paymentDate, setPaymentDate] = useState("2026-03-31");
  const [billingPeriod, setBillingPeriod] = useState<CalendarDateRange>({
    from: "2026-04-01",
    to: "2026-04-30",
  });

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
          errors={schemaProblems(deliveryDateSchema, deliveryDate)}
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
      <Dialog>
        <DialogTrigger asChild>
          <Button type="button" variant="outline" className="w-fit">
            Choose dates in a dialog
          </Button>
        </DialogTrigger>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Dates in a dialog</DialogTitle>
            <DialogDescription>
              Inside a dialog too, each calendar opens with its day focused.
            </DialogDescription>
          </DialogHeader>
          <FieldGroup>
            <FormField id="ks-forms-dialog-payment-date" label="Payment date">
              {(frame) => <DateInput {...frame} value={paymentDate} onValueChange={setPaymentDate} />}
            </FormField>
            <FormField id="ks-forms-dialog-billing-period" label="Billing period">
              {(frame) => (
                <DateRangeInput {...frame} value={billingPeriod} onValueChange={setBillingPeriod} />
              )}
            </FormField>
          </FieldGroup>
          <DialogFooter>
            <DialogClose asChild>
              <Button type="button" variant="outline">
                Done
              </Button>
            </DialogClose>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Specimen>
  );
}
