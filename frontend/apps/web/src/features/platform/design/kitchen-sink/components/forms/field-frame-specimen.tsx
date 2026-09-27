"use client";

import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { FieldGroup } from "@dewiride/erp-ui/components/ui/field";
import { Input } from "@dewiride/erp-ui/components/ui/input";
import { Switch } from "@dewiride/erp-ui/components/ui/switch";
import { useState } from "react";

import { Specimen } from "../specimen";

import { formFieldIds } from "./form-field-ids";

const emailShape = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function FieldFrameSpecimen() {
  const [clientName, setClientName] = useState("Acme Private Limited");
  const [legalName, setLegalName] = useState("");
  const [email, setEmail] = useState("accounts@");
  const [reminders, setReminders] = useState(true);
  const [creditDays, setCreditDays] = useState("30");

  return (
    <Specimen
      title="Field frame"
      description="Label, description and error wired to the control; required, invalid, disabled, horizontal and responsive layouts."
    >
      <FieldGroup>
        <FormField id={formFieldIds.clientName} label="Client name" description="Shown on every invoice.">
          {(frame) => (
            <Input
              {...frame}
              autoComplete="organization"
              value={clientName}
              onChange={(event) => setClientName(event.currentTarget.value)}
            />
          )}
        </FormField>
        <FormField
          id={formFieldIds.legalName}
          label="Legal name"
          description="As printed on the PAN card."
          required
        >
          {(frame) => (
            <Input
              {...frame}
              value={legalName}
              onChange={(event) => setLegalName(event.currentTarget.value)}
            />
          )}
        </FormField>
        <FormField
          id={formFieldIds.email}
          label="Email for remittance advice"
          errors={emailShape.test(email) ? undefined : ["Enter an email address such as accounts@acme.in."]}
        >
          {(frame) => (
            <Input
              {...frame}
              type="email"
              autoComplete="email"
              value={email}
              onChange={(event) => setEmail(event.currentTarget.value)}
            />
          )}
        </FormField>
        <FormField
          id={formFieldIds.companyPan}
          label="Company PAN"
          description="Set by the administrator."
          disabled
        >
          {(frame) => <Input {...frame} value="AAACD1234E" readOnly disabled />}
        </FormField>
        <FormField
          id={formFieldIds.reminders}
          label="Payment reminders"
          description="Remind the client 3 days before the due date."
          orientation="horizontal"
        >
          {(frame) => <Switch {...frame} checked={reminders} onCheckedChange={setReminders} />}
        </FormField>
        <FormField
          id={formFieldIds.creditDays}
          label="Credit days"
          description="Label and control sit side by side where the group is wide enough."
          orientation="responsive"
        >
          {(frame) => (
            <Input
              {...frame}
              inputMode="numeric"
              value={creditDays}
              onChange={(event) => setCreditDays(event.currentTarget.value)}
            />
          )}
        </FormField>
      </FieldGroup>
    </Specimen>
  );
}
