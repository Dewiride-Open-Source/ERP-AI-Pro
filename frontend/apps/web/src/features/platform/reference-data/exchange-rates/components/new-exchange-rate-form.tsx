"use client";

import { FormAlert } from "@dewiride/erp-ui/components/forms/form-alert";
import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { SubmitButton } from "@dewiride/erp-ui/components/forms/submit-button";
import { DateInput } from "@dewiride/erp-ui/components/pickers/date-input";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import { FieldGroup } from "@dewiride/erp-ui/components/ui/field";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useMemo } from "react";
import { Controller } from "react-hook-form";
import { toast } from "sonner";

import { leavesGroup } from "@/shared/forms/client/field-focus";
import { fieldErrorMessages } from "@/shared/forms/client/form-feedback";
import { useActionForm, type FormAction } from "@/shared/forms/client/use-action-form";

import { CurrencyPicker } from "../../currencies/components/currency-picker";
import { exchangeRatesNavigation } from "../../nav";
import {
  indianRupee,
  recordExchangeRateSchema,
  recordExchangeRateValues,
  type RecordExchangeRateValues,
} from "../forms/exchange-rate.schema";
import { recordExchangeRate } from "../server/actions";

import { recordedMessage } from "./exchange-rate-display";
import { RateField, SourceField, UnitField } from "./exchange-rate-fields";

const notForeign: readonly string[] = [indianRupee];

// The recorded rate's page takes the form's place in the history, so Back leads to where the recording started.
export function NewExchangeRateForm({ today, labelledBy }: { today: string; labelledBy: string }) {
  const router = useRouter();
  const schema = useMemo(() => recordExchangeRateSchema(today), [today]);
  const action = useCallback<FormAction<RecordExchangeRateValues>>(
    async (previous, submission) => {
      const answer = await recordExchangeRate(previous, submission);
      if (answer.status === "succeeded") {
        toast.success(recordedMessage(submission.values.currency, submission.values.rateDate));
        router.replace(`/platform/reference-data/exchange-rates/${answer.exchangeRateId}`);
      }
      return answer;
    },
    [router],
  );
  const { form, pending, ready, onSubmit, alertRef, alert, fieldId } = useActionForm({
    schema,
    action,
    defaultValues: recordExchangeRateValues,
    idempotent: true,
  });
  const { control, register } = form;

  return (
    <form
      noValidate
      method="post"
      onSubmit={onSubmit}
      aria-labelledby={labelledBy}
      data-testid="exchange-rate-form"
      className="grid min-w-0 gap-6"
    >
      {alert ? <FormAlert ref={alertRef} {...alert} /> : null}

      <FieldGroup>
        <div className="grid min-w-0 gap-5 md:grid-cols-2">
          <Controller
            control={control}
            name="currency"
            render={({ field, fieldState }) => (
              <FormField
                id={fieldId("currency")}
                label="Currency"
                description="A foreign currency; type its code or name."
                required
                errors={fieldErrorMessages(fieldState.error)}
              >
                {(frame) => (
                  <CurrencyPicker
                    {...frame}
                    ref={field.ref}
                    name={field.name}
                    value={field.value === "" ? null : field.value}
                    onValueChange={(value) => field.onChange(value ?? "")}
                    onBlur={field.onBlur}
                    excluded={notForeign}
                    placeholder="Search currencies"
                  />
                )}
              </FormField>
            )}
          />
          <Controller
            control={control}
            name="rateDate"
            render={({ field, fieldState }) => (
              <FormField
                id={fieldId("rateDate")}
                label="Day"
                description="The day the rate is for: today or an earlier day."
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
                      max={today}
                    />
                  </div>
                )}
              </FormField>
            )}
          />
          <Controller
            control={control}
            name="rate"
            render={({ field, fieldState }) => (
              <RateField id={fieldId("rate")} field={field} error={fieldState.error} />
            )}
          />
          <Controller
            control={control}
            name="unit"
            render={({ field, fieldState }) => (
              <UnitField id={fieldId("unit")} field={field} error={fieldState.error} />
            )}
          />
        </div>
        <SourceField
          id={fieldId("source")}
          registration={register("source")}
          error={form.formState.errors.source}
        />
      </FieldGroup>

      <div className="flex flex-wrap items-center gap-3">
        <SubmitButton pending={pending} ready={ready} data-testid="exchange-rate-save">
          Record the rate
        </SubmitButton>
        <Button asChild variant="outline">
          <Link href={exchangeRatesNavigation.basePath}>Cancel</Link>
        </Button>
      </div>
    </form>
  );
}
