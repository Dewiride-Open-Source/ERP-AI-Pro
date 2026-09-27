"use client";

import { Checkbox } from "@dewiride/erp-ui/components/ui/checkbox";
import {
  Field,
  FieldContent,
  FieldDescription,
  FieldError,
  FieldLabel,
  FieldLegend,
  FieldSet,
  FieldTitle,
} from "@dewiride/erp-ui/components/ui/field";
import { Label } from "@dewiride/erp-ui/components/ui/label";
import { RadioGroup, RadioGroupItem } from "@dewiride/erp-ui/components/ui/radio-group";
import { Slider } from "@dewiride/erp-ui/components/ui/slider";
import { Switch } from "@dewiride/erp-ui/components/ui/switch";
import { useState } from "react";

import { formatRupees } from "@/shared/format/money";

import { Specimen, SpecimenGrid } from "../specimen";

const checkboxes = [
  { id: "ks-checkbox-unchecked", label: "Unchecked", props: {} },
  { id: "ks-checkbox-checked", label: "Checked", props: { defaultChecked: true } },
  {
    id: "ks-checkbox-indeterminate",
    label: "Some rows selected",
    props: { defaultChecked: "indeterminate" },
  },
  { id: "ks-checkbox-disabled", label: "Disabled", props: { disabled: true } },
  {
    id: "ks-checkbox-disabled-checked",
    label: "Disabled and checked",
    props: { disabled: true, defaultChecked: true },
  },
] as const;

export function SelectionShowcase() {
  return (
    <SpecimenGrid>
      <Specimen title="Checkbox" description="Unchecked, checked, indeterminate, disabled and invalid.">
        <div className="grid gap-3">
          {checkboxes.map((checkbox) => (
            <div key={checkbox.id} className="flex items-center gap-2">
              <Checkbox id={checkbox.id} {...checkbox.props} />
              <Label htmlFor={checkbox.id}>{checkbox.label}</Label>
            </div>
          ))}
          <Field orientation="horizontal" data-invalid="true">
            <Checkbox
              id="ks-checkbox-invalid"
              aria-invalid="true"
              aria-describedby="ks-checkbox-invalid-error"
            />
            <FieldContent>
              <FieldLabel htmlFor="ks-checkbox-invalid">I confirm the bank details are correct</FieldLabel>
              <FieldError id="ks-checkbox-invalid-error">Confirm the bank details to continue.</FieldError>
            </FieldContent>
          </Field>
        </div>
      </Specimen>

      <Specimen title="Radio group" description="Arrow keys move and select; one option is disabled.">
        <FieldSet>
          <FieldLegend id="ks-radio-terms-legend" variant="label">
            Payment terms
          </FieldLegend>
          <RadioGroup defaultValue="net-30" aria-labelledby="ks-radio-terms-legend">
            <div className="flex items-center gap-2">
              <RadioGroupItem value="immediate" id="ks-radio-immediate" />
              <Label htmlFor="ks-radio-immediate">Due on receipt</Label>
            </div>
            <div className="flex items-center gap-2">
              <RadioGroupItem value="net-30" id="ks-radio-net-30" />
              <Label htmlFor="ks-radio-net-30">Net 30 days</Label>
            </div>
            <div className="flex items-center gap-2">
              <RadioGroupItem value="net-45" id="ks-radio-net-45" />
              <Label htmlFor="ks-radio-net-45">Net 45 days</Label>
            </div>
            <div className="flex items-center gap-2">
              <RadioGroupItem value="net-90" id="ks-radio-net-90" disabled />
              <Label htmlFor="ks-radio-net-90">Net 90 days (needs approval)</Label>
            </div>
          </RadioGroup>
        </FieldSet>
        <FieldSet data-invalid="true">
          <FieldLegend id="ks-radio-copy-legend" variant="label">
            Invoice copy
          </FieldLegend>
          <RadioGroup
            aria-labelledby="ks-radio-copy-legend"
            aria-describedby="ks-radio-invalid-error"
            aria-invalid="true"
          >
            <div className="flex items-center gap-2">
              <RadioGroupItem value="original" id="ks-radio-original" aria-invalid="true" />
              <Label htmlFor="ks-radio-original">Original for recipient</Label>
            </div>
            <div className="flex items-center gap-2">
              <RadioGroupItem value="duplicate" id="ks-radio-duplicate" aria-invalid="true" />
              <Label htmlFor="ks-radio-duplicate">Duplicate for transporter</Label>
            </div>
          </RadioGroup>
          <FieldError id="ks-radio-invalid-error">Choose which copy to print.</FieldError>
        </FieldSet>
      </Specimen>

      <Specimen title="Switch" description="Off, on, small, disabled and invalid.">
        <div className="grid gap-3">
          <div className="flex items-center gap-2">
            <Switch id="ks-switch-off" />
            <Label htmlFor="ks-switch-off">Email notifications</Label>
          </div>
          <div className="flex items-center gap-2">
            <Switch id="ks-switch-on" defaultChecked />
            <Label htmlFor="ks-switch-on">Round off totals</Label>
          </div>
          <div className="flex items-center gap-2">
            <Switch id="ks-switch-small" size="sm" defaultChecked />
            <Label htmlFor="ks-switch-small">Compact rows</Label>
          </div>
          <div className="flex items-center gap-2">
            <Switch id="ks-switch-disabled" disabled />
            <Label htmlFor="ks-switch-disabled">Reverse charge (not applicable)</Label>
          </div>
          <Field orientation="horizontal" data-invalid="true">
            <Switch id="ks-switch-invalid" aria-invalid="true" aria-describedby="ks-switch-invalid-error" />
            <FieldContent>
              <FieldLabel htmlFor="ks-switch-invalid">Accept the data processing terms</FieldLabel>
              <FieldError id="ks-switch-invalid-error">Accept the terms to continue.</FieldError>
            </FieldContent>
          </Field>
        </div>
      </Specimen>

      <Specimen
        title="Slider"
        description="A range with a thumb for each end; arrow keys move the focused thumb."
      >
        <AmountRangeSlider />
        <Field data-disabled="true">
          <FieldTitle id="ks-slider-disabled-label">Credit limit (₹ lakh)</FieldTitle>
          <div role="group" aria-labelledby="ks-slider-disabled-label">
            <Slider defaultValue={[10, 40]} max={50} disabled />
          </div>
        </Field>
      </Specimen>
    </SpecimenGrid>
  );
}

function AmountRangeSlider() {
  const [range, setRange] = useState([20, 80]);
  const [from = 0, to = 0] = range;

  return (
    <Field>
      <FieldTitle id="ks-slider-range-label">Invoice amount (₹ thousand)</FieldTitle>
      <div
        role="group"
        aria-labelledby="ks-slider-range-label"
        aria-describedby="ks-slider-range-description"
      >
        <Slider value={range} onValueChange={setRange} max={100} step={5} />
      </div>
      <FieldDescription id="ks-slider-range-description">
        From {formatRupees(from * 1000, { paise: false })} to {formatRupees(to * 1000, { paise: false })}.
      </FieldDescription>
    </Field>
  );
}
