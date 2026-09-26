import { Checkbox } from "@dewiride/erp-ui/components/ui/checkbox";
import {
  Field,
  FieldContent,
  FieldDescription,
  FieldError,
  FieldGroup,
  FieldLabel,
  FieldLegend,
  FieldSeparator,
  FieldSet,
} from "@dewiride/erp-ui/components/ui/field";
import { Input } from "@dewiride/erp-ui/components/ui/input";
import {
  InputGroup,
  InputGroupAddon,
  InputGroupInput,
  InputGroupText,
} from "@dewiride/erp-ui/components/ui/input-group";
import { Label } from "@dewiride/erp-ui/components/ui/label";
import { NativeSelect, NativeSelectOption } from "@dewiride/erp-ui/components/ui/native-select";
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectSeparator,
  SelectTrigger,
  SelectValue,
} from "@dewiride/erp-ui/components/ui/select";
import { Spinner } from "@dewiride/erp-ui/components/ui/spinner";
import { Switch } from "@dewiride/erp-ui/components/ui/switch";
import { Textarea } from "@dewiride/erp-ui/components/ui/textarea";
import { SearchIcon } from "lucide-react";

import { Specimen, SpecimenGrid } from "../specimen";

import { ExampleForm } from "./example-form";

const states = [
  { value: "KA", label: "Karnataka" },
  { value: "MH", label: "Maharashtra" },
  { value: "RJ", label: "Rajasthan" },
  { value: "TN", label: "Tamil Nadu" },
] as const;

export function InputsShowcase() {
  return (
    <SpecimenGrid>
      <Specimen title="Label and input" description="Default, disabled, invalid and read-only.">
        <FieldGroup>
          <Field>
            <FieldLabel htmlFor="ks-input-default">Client name</FieldLabel>
            <Input id="ks-input-default" placeholder="Acme Private Limited" autoComplete="organization" />
          </Field>
          <Field data-disabled="true">
            <FieldLabel htmlFor="ks-input-disabled">Company PAN</FieldLabel>
            <Input id="ks-input-disabled" defaultValue="AAACD1234E" disabled />
          </Field>
          <Field data-invalid="true">
            <FieldLabel htmlFor="ks-input-invalid">GSTIN</FieldLabel>
            <Input
              id="ks-input-invalid"
              defaultValue="27AAACD1234E1Z"
              aria-invalid="true"
              aria-describedby="ks-input-invalid-error"
            />
            <FieldError id="ks-input-invalid-error">Enter all 15 characters of the GSTIN.</FieldError>
          </Field>
          <div className="grid gap-2">
            <Label htmlFor="ks-input-read-only">Invoice number</Label>
            <Input id="ks-input-read-only" defaultValue="INV-2026-00042" readOnly />
          </div>
        </FieldGroup>
      </Specimen>

      <Specimen title="Textarea" description="Grows with its content; disabled and invalid states.">
        <FieldGroup>
          <Field>
            <FieldLabel htmlFor="ks-textarea-default">Notes to the client</FieldLabel>
            <Textarea id="ks-textarea-default" placeholder="Thank you for your business." />
          </Field>
          <Field data-disabled="true">
            <FieldLabel htmlFor="ks-textarea-disabled">Terms</FieldLabel>
            <Textarea id="ks-textarea-disabled" defaultValue="Payment within 30 days." disabled />
          </Field>
          <Field data-invalid="true">
            <FieldLabel htmlFor="ks-textarea-invalid">Reason for the credit note</FieldLabel>
            <Textarea
              id="ks-textarea-invalid"
              aria-invalid="true"
              aria-describedby="ks-textarea-invalid-error"
            />
            <FieldError id="ks-textarea-invalid-error">
              Describe why the invoice is being corrected.
            </FieldError>
          </Field>
        </FieldGroup>
      </Specimen>

      <Specimen
        title="Input group"
        description="Add-ons before and after the input, invalid, disabled and loading."
      >
        <FieldGroup>
          <Field>
            <FieldLabel htmlFor="ks-input-group-amount">Amount</FieldLabel>
            <InputGroup>
              <InputGroupAddon>
                <InputGroupText>₹</InputGroupText>
              </InputGroupAddon>
              <InputGroupInput id="ks-input-group-amount" inputMode="decimal" placeholder="0.00" />
            </InputGroup>
          </Field>
          <Field data-invalid="true">
            <FieldLabel htmlFor="ks-input-group-invalid">Discount</FieldLabel>
            <InputGroup>
              <InputGroupInput
                id="ks-input-group-invalid"
                inputMode="decimal"
                defaultValue="120"
                aria-invalid="true"
                aria-describedby="ks-input-group-invalid-error"
              />
              <InputGroupAddon align="inline-end">
                <InputGroupText>%</InputGroupText>
              </InputGroupAddon>
            </InputGroup>
            <FieldError id="ks-input-group-invalid-error">A discount cannot exceed 100 %.</FieldError>
          </Field>
          <Field data-disabled="true">
            <FieldLabel htmlFor="ks-input-group-disabled">Rate</FieldLabel>
            <InputGroup>
              <InputGroupAddon>
                <InputGroupText>₹</InputGroupText>
              </InputGroupAddon>
              <InputGroupInput id="ks-input-group-disabled" defaultValue="2,500.00" disabled />
            </InputGroup>
          </Field>
          <Field>
            <FieldLabel htmlFor="ks-input-group-loading">Search clients</FieldLabel>
            <InputGroup aria-busy="true" data-testid="inputs-loading-group">
              <InputGroupAddon>
                <SearchIcon aria-hidden />
              </InputGroupAddon>
              <InputGroupInput id="ks-input-group-loading" type="search" defaultValue="Acme" />
              <InputGroupAddon align="inline-end">
                <Spinner />
              </InputGroupAddon>
            </InputGroup>
          </Field>
        </FieldGroup>
      </Specimen>

      <Specimen title="Native select" description="The browser's own list; best on mobile for short lists.">
        <FieldGroup>
          <Field>
            <FieldLabel htmlFor="ks-native-select-default">State</FieldLabel>
            <NativeSelect id="ks-native-select-default" defaultValue="MH">
              {states.map((state) => (
                <NativeSelectOption key={state.value} value={state.value}>
                  {state.label}
                </NativeSelectOption>
              ))}
            </NativeSelect>
          </Field>
          <Field data-disabled="true">
            <FieldLabel htmlFor="ks-native-select-disabled">Place of supply</FieldLabel>
            <NativeSelect id="ks-native-select-disabled" defaultValue="KA" disabled>
              {states.map((state) => (
                <NativeSelectOption key={state.value} value={state.value}>
                  {state.label}
                </NativeSelectOption>
              ))}
            </NativeSelect>
          </Field>
          <Field data-invalid="true">
            <FieldLabel htmlFor="ks-native-select-invalid">Billing state</FieldLabel>
            <NativeSelect
              id="ks-native-select-invalid"
              defaultValue=""
              aria-invalid="true"
              aria-describedby="ks-native-select-invalid-error"
            >
              <NativeSelectOption value="">Choose a state</NativeSelectOption>
              {states.map((state) => (
                <NativeSelectOption key={state.value} value={state.value}>
                  {state.label}
                </NativeSelectOption>
              ))}
            </NativeSelect>
            <FieldError id="ks-native-select-invalid-error">Choose the billing state.</FieldError>
          </Field>
        </FieldGroup>
      </Specimen>

      <Specimen title="Select" description="A styled list with groups; disabled and invalid states.">
        <FieldGroup>
          <Field>
            <FieldLabel htmlFor="ks-select-default">Financial quarter</FieldLabel>
            <Select defaultValue="q2">
              <SelectTrigger id="ks-select-default">
                <SelectValue placeholder="Choose a quarter" />
              </SelectTrigger>
              <SelectContent>
                <SelectGroup>
                  <SelectLabel>2026–27</SelectLabel>
                  <SelectItem value="q1">Q1 (April–June)</SelectItem>
                  <SelectItem value="q2">Q2 (July–September)</SelectItem>
                  <SelectItem value="q3">Q3 (October–December)</SelectItem>
                  <SelectItem value="q4" disabled>
                    Q4 (January–March)
                  </SelectItem>
                </SelectGroup>
                <SelectSeparator />
                <SelectGroup>
                  <SelectLabel>2025–26</SelectLabel>
                  <SelectItem value="previous-q4">Q4 (January–March)</SelectItem>
                </SelectGroup>
              </SelectContent>
            </Select>
          </Field>
          <Field data-disabled="true">
            <FieldLabel htmlFor="ks-select-disabled">Currency</FieldLabel>
            <Select defaultValue="INR" disabled>
              <SelectTrigger id="ks-select-disabled">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="INR">INR (₹)</SelectItem>
              </SelectContent>
            </Select>
          </Field>
          <Field data-invalid="true">
            <FieldLabel htmlFor="ks-select-invalid">Tax rate</FieldLabel>
            <Select>
              <SelectTrigger
                id="ks-select-invalid"
                aria-invalid="true"
                aria-describedby="ks-select-invalid-error"
              >
                <SelectValue placeholder="Choose a rate" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="gst-5">GST 5 %</SelectItem>
                <SelectItem value="gst-18">GST 18 %</SelectItem>
              </SelectContent>
            </Select>
            <FieldError id="ks-select-invalid-error">Choose the tax rate for this line.</FieldError>
          </Field>
        </FieldGroup>
      </Specimen>

      <Specimen
        title="Field layouts"
        description="A field set with a legend, horizontal fields and a separator."
      >
        <FieldSet>
          <FieldLegend>Delivery</FieldLegend>
          <FieldDescription>How the invoice reaches the client.</FieldDescription>
          <FieldGroup>
            <Field orientation="horizontal">
              <Checkbox id="ks-field-copy" defaultChecked />
              <FieldLabel htmlFor="ks-field-copy">Send a copy to the accounts team</FieldLabel>
            </Field>
            <FieldSeparator>or</FieldSeparator>
            <Field orientation="horizontal">
              <FieldContent>
                <FieldLabel htmlFor="ks-field-reminders">Payment reminders</FieldLabel>
                <FieldDescription>Remind the client 3 days before the due date.</FieldDescription>
              </FieldContent>
              <Switch id="ks-field-reminders" />
            </Field>
            <Field orientation="horizontal" data-disabled="true">
              <Checkbox id="ks-field-disabled" disabled />
              <FieldLabel htmlFor="ks-field-disabled">Post to the ledger (after approval)</FieldLabel>
            </Field>
          </FieldGroup>
        </FieldSet>
      </Specimen>

      <Specimen
        title="Example form"
        description="Submit it empty to see the error, then enter a name to see the success toast."
        wide
      >
        <ExampleForm />
      </Specimen>
    </SpecimenGrid>
  );
}
