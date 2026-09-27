"use client";

import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { AmountInput } from "@dewiride/erp-ui/components/inputs/amount-input";
import { FieldGroup } from "@dewiride/erp-ui/components/ui/field";
import { useState } from "react";

import { amountSchema } from "@/shared/forms/schemas/field-schemas";

import { Specimen } from "../specimen";

import { schemaProblems } from "./schema-problems";

const discountSchema = amountSchema({ scale: 2, required: false, max: "100000" });

export function AmountSpecimen() {
  const [invoiceAmount, setInvoiceAmount] = useState("12345678.5");
  const [openingBalance, setOpeningBalance] = useState("");
  const [adjustment, setAdjustment] = useState("-2500");
  const [discount, setDiscount] = useState("150000");
  const [roundOff, setRoundOff] = useState("1500000");

  return (
    <Specimen
      title="Amount"
      description="Rupees with a decimal string as the value: plain while typing, grouped in lakh and crore once you leave the field."
    >
      <FieldGroup>
        <FormField
          id="ks-forms-invoice-amount"
          label="Invoice amount"
          description="Up to 15 digits before the decimal point."
        >
          {(frame) => <AmountInput {...frame} value={invoiceAmount} onValueChange={setInvoiceAmount} />}
        </FormField>
        <p className="text-caption text-muted-foreground" data-testid="forms-amount-value">
          Value: <span className="font-mono">{invoiceAmount === "" ? "(empty)" : invoiceAmount}</span>
        </p>
        <FormField id="ks-forms-opening-balance" label="Opening balance" required>
          {(frame) => (
            <AmountInput
              {...frame}
              value={openingBalance}
              onValueChange={setOpeningBalance}
              placeholder="0.00"
            />
          )}
        </FormField>
        <FormField
          id="ks-forms-adjustment"
          label="Adjustment"
          description="A credit note can lower the balance, so this amount may be negative."
        >
          {(frame) => (
            <AmountInput {...frame} value={adjustment} onValueChange={setAdjustment} allowNegative />
          )}
        </FormField>
        <FormField id="ks-forms-discount" label="Discount" errors={schemaProblems(discountSchema, discount)}>
          {(frame) => <AmountInput {...frame} value={discount} onValueChange={setDiscount} />}
        </FormField>
        <FormField id="ks-forms-rate" label="Rate" disabled>
          {(frame) => <AmountInput {...frame} value="2500" onValueChange={() => undefined} disabled />}
        </FormField>
        <FormField id="ks-forms-round-off" label="Rounded total" description="Whole rupees, without paise.">
          {(frame) => <AmountInput {...frame} value={roundOff} onValueChange={setRoundOff} scale={0} />}
        </FormField>
      </FieldGroup>
    </Specimen>
  );
}
