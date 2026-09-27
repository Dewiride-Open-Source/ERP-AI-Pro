"use client";

import { Combobox } from "@dewiride/erp-ui/components/combobox/combobox";
import { FormAlert } from "@dewiride/erp-ui/components/forms/form-alert";
import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { SubmitButton } from "@dewiride/erp-ui/components/forms/submit-button";
import { AmountInput } from "@dewiride/erp-ui/components/inputs/amount-input";
import { IdentifierInput } from "@dewiride/erp-ui/components/inputs/identifier-input";
import { DateInput } from "@dewiride/erp-ui/components/pickers/date-input";
import { FieldGroup } from "@dewiride/erp-ui/components/ui/field";
import { Input } from "@dewiride/erp-ui/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@dewiride/erp-ui/components/ui/select";
import { formatDisplayDate } from "@dewiride/erp-ui/lib/calendar-date";
import { groupIndian } from "@dewiride/erp-ui/lib/indian-number";
import { useState } from "react";
import { Controller } from "react-hook-form";

import { fieldErrorMessages } from "@/shared/forms/client/form-feedback";
import { useActionForm } from "@/shared/forms/client/use-action-form";
import { identifierLengths } from "@/shared/forms/schemas/indian-identifiers";

import {
  agreementStartEarliest,
  agreementStartLatest,
  defaultSupplierExampleValues,
  openingBalanceMaximum,
  serverAnswers,
  supplierExampleSchema,
  supplierStates,
} from "../forms/supplier-example.schema";
import { registerSupplierExample } from "../server/actions";

import { AgreementDocumentField } from "./agreement-document-field";
import { leavesGroup } from "./field-focus";
import { categoryOptions, serverAnswerLabels, stateLabels } from "./supplier-choices";
import { ValidityField } from "./validity-field";

const identifierFields = [
  {
    name: "gstin",
    label: "GSTIN",
    description: "15 characters. Spaces and dashes are removed as you type.",
    maxLength: identifierLengths.gstin,
  },
  { name: "pan", label: "PAN", description: "10 characters.", maxLength: identifierLengths.pan },
  {
    name: "ifsc",
    label: "IFSC of the supplier's bank",
    description: "11 characters.",
    maxLength: identifierLengths.ifsc,
  },
] as const;

export function SupplierExampleForm({ labelledBy }: { labelledBy: string }) {
  const { form, state, pending, ready, onSubmit, alertRef, alert, fieldId, idempotencyKey } = useActionForm({
    schema: supplierExampleSchema,
    action: registerSupplierExample,
    defaultValues: defaultSupplierExampleValues,
    idempotent: true,
  });
  const [uploading, setUploading] = useState(false);
  const {
    control,
    register,
    formState: { errors, isSubmitSuccessful },
  } = form;
  const saved = !pending && isSubmitSuccessful && state.status === "succeeded" ? state.message : undefined;

  return (
    <form
      noValidate
      method="post"
      onSubmit={onSubmit}
      aria-labelledby={labelledBy}
      data-testid="supplier-example-form"
      className="grid min-w-0 gap-6"
    >
      {alert ? <FormAlert ref={alertRef} {...alert} /> : null}

      <FieldGroup>
        <FormField
          id={fieldId("legalName")}
          label="Legal name"
          description="As printed on the supplier's PAN card."
          required
          errors={fieldErrorMessages(errors.legalName)}
        >
          {(frame) => <Input {...frame} {...register("legalName")} autoComplete="organization" />}
        </FormField>

        <div className="grid min-w-0 gap-5 md:grid-cols-3">
          {identifierFields.map((identifier) => (
            <Controller
              key={identifier.name}
              control={control}
              name={identifier.name}
              render={({ field, fieldState }) => (
                <FormField
                  id={fieldId(identifier.name)}
                  label={identifier.label}
                  description={identifier.description}
                  required
                  errors={fieldErrorMessages(fieldState.error)}
                >
                  {(frame) => (
                    <IdentifierInput
                      {...frame}
                      ref={field.ref}
                      name={field.name}
                      value={field.value}
                      onValueChange={field.onChange}
                      onBlur={field.onBlur}
                      maxLength={identifier.maxLength}
                    />
                  )}
                </FormField>
              )}
            />
          ))}
        </div>

        <div className="grid min-w-0 gap-5 md:grid-cols-2">
          <Controller
            control={control}
            name="state"
            render={({ field, fieldState }) => (
              <FormField
                id={fieldId("state")}
                label="State of the registered office"
                required
                errors={fieldErrorMessages(fieldState.error)}
              >
                {(frame) => (
                  <Select
                    value={field.value}
                    onValueChange={field.onChange}
                    onOpenChange={(open) => {
                      if (!open) field.onBlur();
                    }}
                  >
                    <SelectTrigger {...frame} ref={field.ref} className="w-full">
                      <SelectValue placeholder="Choose a state" />
                    </SelectTrigger>
                    <SelectContent>
                      {supplierStates.map((value) => (
                        <SelectItem key={value} value={value}>
                          {stateLabels[value]}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              </FormField>
            )}
          />
          <Controller
            control={control}
            name="category"
            render={({ field, fieldState }) => (
              <FormField
                id={fieldId("category")}
                label="Category"
                description="Type to narrow the list."
                required
                errors={fieldErrorMessages(fieldState.error)}
              >
                {(frame) => (
                  <Combobox
                    {...frame}
                    ref={field.ref}
                    name={field.name}
                    options={categoryOptions}
                    value={field.value === "" ? null : field.value}
                    onValueChange={(value) => field.onChange(value ?? "")}
                    onBlur={field.onBlur}
                    placeholder="Search categories"
                    emptyMessage="No category matches."
                  />
                )}
              </FormField>
            )}
          />
        </div>

        <div className="grid min-w-0 gap-5 md:grid-cols-2">
          <Controller
            control={control}
            name="openingBalance"
            render={({ field, fieldState }) => (
              <FormField
                id={fieldId("openingBalance")}
                label="Opening balance"
                description={`Optional. What the company owes the supplier today, up to ₹${groupIndian(openingBalanceMaximum, 2)}.`}
                errors={fieldErrorMessages(fieldState.error)}
              >
                {(frame) => (
                  <AmountInput
                    {...frame}
                    ref={field.ref}
                    name={field.name}
                    value={field.value}
                    onValueChange={field.onChange}
                    onBlur={field.onBlur}
                    placeholder="0.00"
                  />
                )}
              </FormField>
            )}
          />
          <Controller
            control={control}
            name="agreementStart"
            render={({ field, fieldState }) => (
              <FormField
                id={fieldId("agreementStart")}
                label="Agreement starts on"
                description={`Between ${formatDisplayDate(agreementStartEarliest)} and ${formatDisplayDate(agreementStartLatest)}.`}
                required
                errors={fieldErrorMessages(fieldState.error)}
              >
                {(frame) => (
                  <div
                    className="min-w-0"
                    onBlur={(event) => {
                      if (leavesGroup(event)) field.onBlur();
                    }}
                  >
                    <DateInput
                      {...frame}
                      ref={field.ref}
                      name={field.name}
                      value={field.value}
                      onValueChange={field.onChange}
                      min={agreementStartEarliest}
                      max={agreementStartLatest}
                    />
                  </div>
                )}
              </FormField>
            )}
          />
        </div>

        <ValidityField control={control} fromId={fieldId("validity.from")} toId={fieldId("validity.to")} />

        <Controller
          control={control}
          name="agreementDocumentId"
          render={({ field, fieldState }) => (
            <AgreementDocumentField
              ref={field.ref}
              id={fieldId("agreementDocumentId")}
              value={field.value}
              onValueChange={field.onChange}
              onUploadingChange={setUploading}
              errors={fieldErrorMessages(fieldState.error) ?? []}
            />
          )}
        />

        <Controller
          control={control}
          name="serverAnswer"
          render={({ field, fieldState }) => (
            <FormField
              id={fieldId("serverAnswer")}
              label="Server answer"
              description="For this page only: how the example Server Function answers the next save. Nothing reaches the API."
              errors={fieldErrorMessages(fieldState.error)}
              className="rounded-lg border border-dashed p-4"
            >
              {(frame) => (
                <Select
                  value={field.value}
                  onValueChange={field.onChange}
                  onOpenChange={(open) => {
                    if (!open) field.onBlur();
                  }}
                >
                  <SelectTrigger {...frame} ref={field.ref} className="w-full md:w-fit">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {serverAnswers.map((value) => (
                      <SelectItem key={value} value={value}>
                        {serverAnswerLabels[value]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            </FormField>
          )}
        />
      </FieldGroup>

      <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
        <SubmitButton
          pending={pending}
          ready={ready}
          disabled={uploading}
          data-testid="supplier-example-save"
        >
          Save
        </SubmitButton>
        <p role="status" data-testid="supplier-example-outcome" className="text-sm text-success">
          {saved}
        </p>
      </div>
      <p data-testid="supplier-example-idempotency-key" className="text-caption text-muted-foreground">
        {idempotencyKey === undefined ? (
          "No idempotency key sent yet."
        ) : (
          <>
            Idempotency key of the last save ends in{" "}
            <span className="font-mono">{idempotencyKey.slice(-8)}</span>.
          </>
        )}
      </p>
    </form>
  );
}
